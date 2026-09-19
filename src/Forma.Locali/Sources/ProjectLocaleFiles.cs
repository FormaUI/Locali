namespace Forma.Locali.Sources;

using System.Globalization;

using Forma.Framework.Localization.Files;
using Forma.Framework.Localization.Json;
using Forma.Locali.Documents;
using Forma.Locali.Projects;

/// <summary>Resolves a project's locale files against the types the runtime finds them by.</summary>
public static class ProjectLocaleFiles
{
    /// <summary>Every readable culture-named file the project embeds, resolved against the compilation, or null when the project would not load.</summary>
    /// <param name="project">The project the files belong to.</param>
    /// <param name="localizationFiles">The locale files this project owns, which the target attributed.</param>
    public static ProjectLocalization? Resolve(LoadedProject project, IReadOnlyCollection<string> localizationFiles)
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
        var files = new List<ProjectLocaleFile>();

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
            if (Read(format, fullPath, culture) is not { } read)
            {
                continue;
            }

            files.Add(new ProjectLocaleFile(fullPath, CultureInfo.GetCultureInfo(culture), name, read));
        }

        return new ProjectLocalization(compilation.AssemblyName!, ResourceTypeNames.GetAll(compilation), files, GetConfigPaths(project), format);
    }

    // The runtime reads the config files an assembly embeds beside its translations, so a folder's own settings decide what is read there.
    private static Dictionary<string, string> GetConfigPaths(LoadedProject project)
    {
        var configPaths = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (path, name) in project.EmbeddedResourceNames ?? new Dictionary<string, string>())
        {
            if (Path.GetFileName(path).Equals(LocaleConfig.FileName, StringComparison.OrdinalIgnoreCase))
            {
                configPaths[name] = path;
            }
        }

        return configPaths;
    }

    // The SDK embeds a file as {RootNamespace}.{RelativeFolderPath}.{FileName}, and the runtime reads a satellite's file back under the same name.
    private static string GetDefaultResourceName(string root, string projectDirectory, string fullPath)
    {
        var relative = Path.GetRelativePath(projectDirectory, fullPath);

        return string.Join('.', [root, .. relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)]);
    }

    private static LocaleFile? Read(JsonLocaleFileFormat format, string path, string culture)
    {
        try
        {
            using var stream = File.OpenRead(path);

            return format.Read(stream, path, culture);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (LocaleFileException)
        {
            return null;
        }
    }
}
