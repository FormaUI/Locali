namespace Forma.Locali.Sources;

using Forma.Framework.Localization.Files;
using Forma.Locali.Documents;
using Forma.Locali.Projects;

/// <summary>Resolves the files in a project's host folder against the anchors the runtime places them at.</summary>
/// <remarks>The host's folder is <c>.locales</c> at the project's root, a folder in it for each assembly whose strings the host rewords; a file the project leaves unembedded is read from the folder copied beside the application. The anchors are counted from each assembly's name, whatever root namespace the assembly declares.</remarks>
public static class HostLocaleFiles
{
    /// <summary>Every culture-named file in the host's folder the project does not embed, with the anchors the runtime places it at.</summary>
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
            if (resourceNames.ContainsKey(fullPath) || !fullPath.StartsWith(hostPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // The first folder is the assembly's; a file above every assembly's folder belongs to none.
            var name = fullPath[hostPrefix.Length..].Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (LocalizationFileName.Read(name, format.Extension) is not { Culture: var culture })
            {
                continue;
            }

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
