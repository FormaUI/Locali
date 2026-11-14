namespace Forma.Locali.Sources;

using System.Globalization;

using Forma.Framework.Localization.Files;
using Forma.Locali.Documents;
using Forma.Locali.Projects;

using Microsoft.CodeAnalysis;

/// <summary>Resolves a locale pack's files against the projects of the run, by the assemblies their names start with.</summary>
public static class LocalePackFiles
{
    /// <summary>The pack's files, each resolved to the longest assembly name the run builds or references that its name starts with, or null when the project is not a locale pack or its items were not read.</summary>
    /// <param name="format">The format the files are written in, read as the runtime reads it.</param>
    /// <param name="project">The project the files belong to.</param>
    /// <param name="localizationFiles">The locale files this project owns, which the target attributed.</param>
    /// <param name="projects">Every project the run loaded, which the files are resolved against.</param>
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

        var built = new Dictionary<string, LoadedProject>(StringComparer.Ordinal);
        var known = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in projects.All)
        {
            if (candidate.Compilation is not { } compilation)
            {
                continue;
            }

            if (compilation.AssemblyName is { Length: > 0 } assemblyName)
            {
                built.TryAdd(assemblyName, candidate);
                known.Add(assemblyName);
            }

            known.UnionWith(compilation.ReferencedAssemblyNames.Select(identity => identity.Name));
        }

        (string Name, string[] Parts)[] knownNames = [.. known.Order(StringComparer.Ordinal).Select(assemblyName => (assemblyName, assemblyName.Split('.')))];
        var assemblies = new Dictionary<string, List<ProjectLocaleFile>>(StringComparer.Ordinal);
        var fileNames = new Dictionary<string, PackFileName>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in localizationFiles)
        {
            var fullPath = Path.GetFullPath(file);
            if (!resourceNames.TryGetValue(fullPath, out var name) || LocalizationFileName.Read(name, format.Extension) is not { } fileName)
            {
                continue;
            }

            // The runtime reads a file for every assembly its name starts with, the culture standing before the name or after it.
            var packFileName = new PackFileName(name, fileName, [.. knownNames.Where(known => fileName.Parts.AsSpan().StartsWith(known.Parts)).Select(known => known.Name)]);
            fileNames[fullPath] = packFileName;

            // A file is held to the types of the assembly its name spells longest, since a shorter one reads it as another assembly's namespace.
            if (packFileName.AssemblyName is not { } assemblyName)
            {
                continue;
            }

            if (!assemblies.TryGetValue(assemblyName, out var files))
            {
                assemblies[assemblyName] = files = [];
            }

            // A file that will not read is the file rules' business, and its name still names its assembly.
            if (ProjectLocaleFiles.Read(format, fullPath, fileName.Culture) is { } read)
            {
                files.Add(new ProjectLocaleFile(fullPath, CultureInfo.GetCultureInfo(fileName.Culture), name, read));
            }
        }

        return new LocalePack(
            project,
            [.. assemblies
                .OrderBy(assembly => assembly.Key, StringComparer.Ordinal)
                .Select(assembly => new LocalePackAssembly(assembly.Key, assembly.Value, built.GetValueOrDefault(assembly.Key)))],
            fileNames,
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
