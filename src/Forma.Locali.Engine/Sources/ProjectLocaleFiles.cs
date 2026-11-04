namespace Forma.Locali.Sources;

using System.Globalization;

using Forma.Framework.Localization.Files;
using Forma.Locali.Documents;
using Forma.Locali.Projects;

/// <summary>Resolves a project's locale files against the types the runtime finds them by.</summary>
public static class ProjectLocaleFiles
{
    /// <summary>Every readable culture-named file the project embeds, and every pack folder naming its assembly, resolved against the compilation, or null when the project would not load or is a locale pack, whose files are other assemblies'.</summary>
    /// <param name="format">The format the files are written in, read as the runtime reads it.</param>
    /// <param name="project">The project the files belong to.</param>
    /// <param name="localizationFiles">The locale files this project owns, which the target attributed.</param>
    /// <param name="packs">Every locale pack of the run, none unless given.</param>
    public static ProjectLocalization? Resolve(ILocaleFileFormat format, LoadedProject project, IReadOnlyCollection<string> localizationFiles, IReadOnlyList<LocalePack>? packs = null)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(localizationFiles);

        // A pack embeds its files for the assemblies its folders are named for, and declares no type of its own to read them.
        if (project.IsLocalePack || project.Compilation is not { AssemblyName.Length: > 0 } compilation)
        {
            return null;
        }

        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(project.Path))!;

        // The SDK embeds under RootNamespace, which a project may declare apart from its assembly name.
        var root = project.RootNamespace ?? compilation.AssemblyName!;
        var files = new List<ProjectLocaleFile>();

        foreach (var file in localizationFiles)
        {
            if (LocalizationFileName.GetCulture(file) is not { } culture)
            {
                continue;
            }

            var fullPath = Path.GetFullPath(file);

            // A file the project does not embed, or embeds under the host's prefix, is the host's, and translates other assemblies' types.
            string name;
            if (project.EmbeddedResourceNames is { } resourceNames)
            {
                if (!resourceNames.TryGetValue(fullPath, out var resourceName) || HostLocaleFiles.IsHostResourceName(resourceName))
                {
                    continue;
                }

                name = resourceName;
            }
            else if (GetDefaultResourceName(root, projectDirectory, fullPath) is { } defaultName)
            {
                name = defaultName;
            }
            else
            {
                continue;
            }

            // A file that will not read is the file rules' business, and the runtime reads nothing from it.
            if (Read(format, fullPath, culture) is not { } read)
            {
                continue;
            }

            files.Add(new ProjectLocaleFile(fullPath, CultureInfo.GetCultureInfo(culture), name, read));
        }

        var source = new ProjectLocaleSource(files, GetConfigPaths(project), format);

        // A pack is read whole for the project, as the runtime reads it, laid out by the root namespace the project's own config declares.
        List<PackLocalization> packLocalizations = [];
        foreach (var pack in packs ?? [])
        {
            foreach (var folder in pack.Folders.Where(folder => folder.Target == project))
            {
                packLocalizations.Add(new PackLocalization(pack, folder, new PackLocaleSource(GetPackName(pack), pack.Files, source.GetRootNamespace, format)));
            }
        }

        return new ProjectLocalization(compilation.AssemblyName!, ResourceTypeNames.GetAll(compilation), files, source, packLocalizations);
    }

    // The runtime reports a pack's file under the pack assembly's name, which its project file also spells where it did not compile.
    private static string GetPackName(LocalePack pack)
        => pack.Project.Compilation?.AssemblyName is { Length: > 0 } assemblyName ? assemblyName : Path.GetFileNameWithoutExtension(pack.Project.Path);

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
    // The SDK rewrites a folder it cannot embed as written into an identifier, by rules only the build knows, so such a file is given no name.
    private static string? GetDefaultResourceName(string root, string projectDirectory, string fullPath)
    {
        var relative = Path.GetRelativePath(projectDirectory, fullPath);
        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return segments[..^1].All(IsEmbeddedAsWritten) ? string.Join('.', [root, .. segments]) : null;
    }

    // A folder is embedded as written when each part between its dots is empty or an identifier of letters, digits and underscores.
    private static bool IsEmbeddedAsWritten(string folder)
    {
        foreach (var part in folder.Split('.'))
        {
            if (part.Length > 0 && (char.IsDigit(part[0]) || !part.All(character => char.IsLetterOrDigit(character) || character == '_')))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The file as the runtime reads it, or null when it cannot be opened or will not read, which is the file rules' business.</summary>
    /// <param name="format">The format the file is written in.</param>
    /// <param name="path">The file, as a full path.</param>
    /// <param name="culture">The culture the file's own name gives it.</param>
    public static LocaleFile? Read(ILocaleFileFormat format, string path, string culture)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(culture);

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
