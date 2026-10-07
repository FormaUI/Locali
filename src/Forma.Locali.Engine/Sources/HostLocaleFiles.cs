namespace Forma.Locali.Sources;

using Forma.Framework.Localization.Files;
using Forma.Locali.Documents;
using Forma.Locali.Projects;

/// <summary>Resolves the files in a project's host folder against the anchors the runtime places them at.</summary>
public static class HostLocaleFiles
{
    /// <summary>Every culture-named file in the host's folder the project does not embed, with the anchors the runtime places it at.</summary>
    /// <remarks>The host's folder is the one the runtime reads by default, <c>.locales</c> at the project's root, copied beside the application rather than embedded; the anchors are counted from each assembly's name, which the two forms a host may write a file in share whatever root namespace the assembly declares.</remarks>
    /// <param name="format">The format the files are written in, read as the runtime reads it.</param>
    /// <param name="project">The project whose folder holds the host's files.</param>
    /// <param name="localizationFiles">The locale files this project owns, which the target attributed.</param>
    public static IReadOnlyList<PlacedLocaleFile> Resolve(ILocaleFileFormat format, LoadedProject project, IReadOnlyCollection<string> localizationFiles)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(localizationFiles);

        // Without the project's items, every file is read as embedded, so none is the host's.
        if (project.EmbeddedResourceNames is not { } resourceNames)
        {
            return [];
        }

        var hostFolder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(project.Path))!, LocaleConfig.DefaultLocalesDir);
        var hostPrefix = hostFolder + Path.DirectorySeparatorChar;
        var source = new HostLocaleSource(hostFolder, format, static _ => null);
        var files = new List<PlacedLocaleFile>();

        foreach (var file in localizationFiles)
        {
            var fullPath = Path.GetFullPath(file);
            if (resourceNames.ContainsKey(fullPath)
                || !fullPath.StartsWith(hostPrefix, StringComparison.OrdinalIgnoreCase)
                || LocalizationFileName.GetCulture(fullPath) is not { } culture)
            {
                continue;
            }

            // The first folder is the assembly's; a file above every assembly's folder belongs to none.
            var name = fullPath[hostPrefix.Length..].Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var assemblyName = name[..Math.Max(name.IndexOf(Path.AltDirectorySeparatorChar, StringComparison.Ordinal), 0)];
            if (assemblyName.Length == 0)
            {
                continue;
            }

            files.Add(new PlacedLocaleFile(fullPath, culture, source.GetAnchorNames(assemblyName, name, culture)));
        }

        return files;
    }
}
