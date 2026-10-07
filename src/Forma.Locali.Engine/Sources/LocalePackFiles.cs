namespace Forma.Locali.Sources;

using System.Globalization;

using Forma.Framework.Localization.Files;
using Forma.Locali.Documents;
using Forma.Locali.Projects;

using Microsoft.CodeAnalysis;

/// <summary>Resolves a locale pack's files into the folders the runtime reads them by, one per assembly, against the projects of the run.</summary>
public static class LocalePackFiles
{
    /// <summary>The pack's folders, each named for an assembly and resolved to the project in the run that builds it, or null when the project is not a locale pack or its items were not read.</summary>
    /// <param name="format">The format the files are written in, read as the runtime reads it.</param>
    /// <param name="project">The project the files belong to.</param>
    /// <param name="localizationFiles">The locale files this project owns, which the target attributed.</param>
    /// <param name="projects">Every project the run loaded, which the folders are resolved against.</param>
    public static LocalePack? Resolve(ILocaleFileFormat format, LoadedProject project, IReadOnlyCollection<string> localizationFiles, LoadedProjects projects)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(localizationFiles);
        ArgumentNullException.ThrowIfNull(projects);

        // Without the project's items no file is known to be embedded, and the runtime reads a pack's files by the names the build gives them.
        if (!project.IsLocalePack || project.EmbeddedResourceNames is not { } resourceNames)
        {
            return null;
        }

        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(project.Path))!;
        var folders = new Dictionary<string, List<ProjectLocaleFile>>(StringComparer.Ordinal);

        foreach (var file in localizationFiles)
        {
            if (LocalizationFileName.GetCulture(file) is not { } culture)
            {
                continue;
            }

            var fullPath = Path.GetFullPath(file);
            if (!resourceNames.TryGetValue(fullPath, out var name))
            {
                continue;
            }

            // The first folder beneath the pack is the assembly's; a file above every assembly's folder belongs to none.
            var relative = Path.GetRelativePath(projectDirectory, fullPath);
            var separator = relative.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
            if (separator <= 0)
            {
                continue;
            }

            var assemblyName = relative[..separator];
            if (!folders.TryGetValue(assemblyName, out var files))
            {
                folders[assemblyName] = files = [];
            }

            // A file that will not read is the file rules' business, and its folder still names an assembly.
            if (ProjectLocaleFiles.Read(format, fullPath, culture) is { } read)
            {
                files.Add(new ProjectLocaleFile(fullPath, CultureInfo.GetCultureInfo(culture), name, read));
            }
        }

        var built = new Dictionary<string, LoadedProject>(StringComparer.Ordinal);
        var referenced = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in projects.All)
        {
            if (candidate.Compilation is not { } compilation)
            {
                continue;
            }

            if (compilation.AssemblyName is { Length: > 0 } assemblyName)
            {
                built.TryAdd(assemblyName, candidate);
            }

            referenced.UnionWith(compilation.ReferencedAssemblyNames.Select(identity => identity.Name));
        }

        return new LocalePack(
            project,
            [.. folders
                .OrderBy(folder => folder.Key, StringComparer.Ordinal)
                .Select(folder => new LocalePackFolder(folder.Key, folder.Value, built.GetValueOrDefault(folder.Key), referenced.Contains(folder.Key)))],
            projects.IsWholeSolution && projects.All.All(candidate => candidate.Compilation is not null));
    }

    /// <summary>Every locale pack of the run, each resolved against the run's projects.</summary>
    /// <param name="format">The format the files are written in, read as the runtime reads it.</param>
    /// <param name="projects">Every project the run loaded.</param>
    /// <param name="localizationFilesOf">The locale files a project owns, by the project's path.</param>
    public static IReadOnlyList<LocalePack> ResolveAll(ILocaleFileFormat format, LoadedProjects projects, Func<string, IReadOnlyList<string>> localizationFilesOf)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(localizationFilesOf);

        return [.. projects.All
            .Where(project => project.IsLocalePack)
            .Select(project => Resolve(format, project, localizationFilesOf(project.Path), projects))
            .OfType<LocalePack>()];
    }
}
