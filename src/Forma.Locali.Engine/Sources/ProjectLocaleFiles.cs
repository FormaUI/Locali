namespace Forma.Locali.Sources;

using System.Globalization;

using Forma.Framework.Localization;
using Forma.Framework.Localization.Files;
using Forma.Locali.Documents;
using Forma.Locali.Projects;

using Microsoft.CodeAnalysis;

/// <summary>Resolves a project's locale files against the types the runtime finds them by.</summary>
public static class ProjectLocaleFiles
{
    /// <summary>Every readable culture-named file the project embeds, and every pack's files naming its assembly, resolved against the compilation, or null when the project would not load or is a locale pack, whose files are other assemblies'.</summary>
    /// <param name="format">The format the files are written in, read as the runtime reads it.</param>
    /// <param name="project">The project the files belong to.</param>
    /// <param name="localizationFiles">The locale files this project owns, which the target attributed.</param>
    /// <param name="packs">Every locale pack of the run, none unless given.</param>
    /// <param name="builtAssemblyNames">The name of every assembly a project of the run builds, none unless given.</param>
    public static ProjectLocalization? Resolve(
        ILocaleFileFormat format,
        LoadedProject project,
        IReadOnlyCollection<string> localizationFiles,
        IReadOnlyList<LocalePack>? packs = null,
        IReadOnlyCollection<string>? builtAssemblyNames = null)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(localizationFiles);

        // A pack embeds its files for the assemblies their names start with, and declares no type of its own to read them.
        if (project.IsLocalePack || project.Compilation is not { AssemblyName.Length: > 0 } compilation)
        {
            return null;
        }

        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(project.Path))!;

        // The build embeds under RootNamespace, which a project may declare apart from its assembly name.
        var root = project.RootNamespace ?? compilation.AssemblyName!;
        var files = new List<ProjectLocaleFile>();

        foreach (var file in localizationFiles)
        {
            var fullPath = Path.GetFullPath(file);

            // A file the project does not embed is the host's, and localizes other assemblies' types from the folder beside the application.
            string name;
            if (project.EmbeddedResourceNames is { } resourceNames)
            {
                if (!resourceNames.TryGetValue(fullPath, out var resourceName))
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

            // The runtime reads a file's culture from the name it is embedded under.
            if (LocalizationFileName.Read(name, format.Extension) is not { } fileName)
            {
                continue;
            }

            // A file that will not read is the file rules' business, and the runtime reads nothing from it.
            if (Read(format, fullPath, fileName.Culture) is not { } read)
            {
                continue;
            }

            files.Add(new ProjectLocaleFile(fullPath, CultureInfo.GetCultureInfo(fileName.Culture), name, read));
        }

        var source = new ProjectLocaleSource(files, GetConfigPaths(project), format);

        // A pack is read whole for the project, as the runtime reads it, laid out by the root namespace the project's own config declares.
        List<PackLocalization> packLocalizations = [];
        foreach (var pack in packs ?? [])
        {
            foreach (var assembly in pack.Assemblies.Where(assembly => assembly.Target == project))
            {
                packLocalizations.Add(new PackLocalization(pack, assembly, new PackLocaleSource(GetPackName(pack), pack.Files, source.GetRootNamespace, format)));
            }
        }

        return new ProjectLocalization(
            compilation.AssemblyName!,
            ResourceTypeNames.GetAll(compilation),
            files,
            source,
            packLocalizations,
            IsApplication(compilation),
            [.. compilation.ReferencedAssemblyNames.Select(identity => identity.Name), .. builtAssemblyNames ?? []]);
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

    // The runtime reads the assembly that runs as the application, or, where none runs, the one its build marked.
    private static bool IsApplication(Compilation compilation)
        => compilation.Options.OutputKind is OutputKind.ConsoleApplication or OutputKind.WindowsApplication or OutputKind.WindowsRuntimeApplication
            || compilation.Assembly.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == typeof(LocaleApplicationAttribute).FullName);

    // The build embeds every locale file in a folder of locale files or outside any folder whose name starts with a dot, as
    // {RootNamespace}.{path} with every folder joined by a dot.
    private static string? GetDefaultResourceName(string root, string projectDirectory, string fullPath)
    {
        var segments = Path.GetRelativePath(projectDirectory, fullPath).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var folders = segments[..^1];

        if (!folders.Contains(LocaleConfig.DefaultLocalesDir, StringComparer.Ordinal) && folders.Any(folder => folder.StartsWith('.', StringComparison.Ordinal)))
        {
            return null;
        }

        return string.Join('.', [root, .. segments]);
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
