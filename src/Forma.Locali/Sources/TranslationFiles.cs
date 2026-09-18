namespace Forma.Locali.Sources;

using System.Globalization;
using System.Text.Json;

using Forma.Framework.Localization.Files;
using Forma.Framework.Localization.Json;
using Forma.Locali.Documents;
using Forma.Locali.Projects;
using Forma.Locali.Rules;

/// <summary>Resolves a project's translation files against the types the runtime finds them by.</summary>
public static class TranslationFiles
{
    /// <summary>Every readable culture-named file the project embeds, resolved against the compilation, or null when the project would not load.</summary>
    /// <param name="project">The project the files belong to.</param>
    /// <param name="localizationFiles">The translation files this project owns, which the target attributed.</param>
    public static ProjectTranslations? Resolve(LoadedProject project, IReadOnlyCollection<string> localizationFiles)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(localizationFiles);

        if (project.Compilation is not { AssemblyName.Length: > 0 } compilation)
        {
            return null;
        }

        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(project.Path))!;

        // The SDK embeds under RootNamespace, which a project may declare apart from its assembly name.
        var root = project.RootNamespace ?? compilation.AssemblyName!;
        var format = new JsonLocaleFileFormat();
        var files = new List<TranslationFile>();

        foreach (var file in localizationFiles)
        {
            if (LocalizationFileName.GetCulture(file) is not { } culture)
            {
                continue;
            }

            var fullPath = Path.GetFullPath(file);

            // A file the project does not embed is the host's own, copied beside the application, and translates other assemblies' types.
            string name;
            if (project.EmbeddedResourceNames is { } resourceNames)
            {
                if (!resourceNames.TryGetValue(fullPath, out var resourceName))
                {
                    continue;
                }

                name = resourceName;
            }
            else
            {
                name = GetDefaultResourceName(root, projectDirectory, fullPath);
            }

            // A file that will not read is the file rules' business, and the runtime reads nothing from it.
            if (Read(format, fullPath, culture) is not var (read, ignored))
            {
                continue;
            }

            files.Add(new TranslationFile(fullPath, CultureInfo.GetCultureInfo(culture), name, read, ignored));
        }

        return new ProjectTranslations(compilation.AssemblyName!, ResourceTypeNames.GetAll(compilation), files, GetSettingsPaths(project), format);
    }

    // The runtime reads the settings files an assembly embeds beside its translations, so a folder's own settings decide what is read there.
    private static Dictionary<string, string> GetSettingsPaths(LoadedProject project)
    {
        var settingsPaths = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (path, name) in project.EmbeddedResourceNames ?? new Dictionary<string, string>())
        {
            if (Path.GetFileName(path).Equals(LocaliSettings.FileName, StringComparison.OrdinalIgnoreCase))
            {
                settingsPaths[name] = path;
            }
        }

        return settingsPaths;
    }

    // The SDK embeds a file as {RootNamespace}.{RelativeFolderPath}.{FileName}, and the runtime reads a satellite's file back under the same name.
    private static string GetDefaultResourceName(string root, string projectDirectory, string fullPath)
    {
        var relative = Path.GetRelativePath(projectDirectory, fullPath);

        return string.Join('.', [root, .. relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)]);
    }

    // The tools entry is the linter's own, which the runtime's format leaves out, so the same bytes are read as a document for it.
    private static (LocaleFile File, IgnoredRules Ignored)? Read(JsonLocaleFileFormat format, string path, string culture)
    {
        try
        {
            var content = File.ReadAllBytes(path);
            using var stream = new MemoryStream(content, writable: false);

            var file = format.Read(stream, path, culture);
            var ignored = LocalizationDocument.TryRead(path, content, out var document, out _) ? IgnoredRules.Read(document!) : IgnoredRules.None;

            return (file, ignored);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (LocalizationMetadataException)
        {
            return null;
        }
    }
}
