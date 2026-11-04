namespace Forma.Locali.Sources;

using Forma.Framework.Localization.Files;
using Forma.Locali.Documents;
using Forma.Locali.Projects;

/// <summary>Resolves the files in a project's host folder against the anchors the runtime places them at.</summary>
/// <remarks>The host's folder is <c>.locales</c> at the project's root, a folder in it for each assembly whose strings the host rewords; an application's build embeds its files under <see cref="LocalePackSource.HostResourcePrefix"/>, and a file the project leaves unembedded is read from the folder copied beside the application. The runtime reads the two forms as two sources, the embedded files first. The anchors are counted from each assembly's name, which the two forms a host may write a file in share whatever root namespace the assembly declares.</remarks>
public static class HostLocaleFiles
{
    /// <summary>Whether a resource name is one the build embeds a host's file under, which the runtime reads as the host's rather than as the assembly's own.</summary>
    /// <param name="resourceName">The name the build embeds a file under.</param>
    public static bool IsHostResourceName(string resourceName)
    {
        ArgumentNullException.ThrowIfNull(resourceName);

        return resourceName.StartsWith(LocalePackSource.HostResourcePrefix, StringComparison.Ordinal);
    }

    /// <summary>Every culture-named file in the host's folder the project does not embed, with the anchors the runtime places it at.</summary>
    /// <param name="format">The format the files are written in, read as the runtime reads it.</param>
    /// <param name="project">The project whose folder holds the host's files.</param>
    /// <param name="localizationFiles">The locale files this project owns, which the target attributed.</param>
    public static IReadOnlyList<PlacedLocaleFile> Resolve(ILocaleFileFormat format, LoadedProject project, IReadOnlyCollection<string> localizationFiles)
        => Place(format, project, localizationFiles, static resourceName => resourceName is null);

    /// <summary>Every culture-named file in the host's folder the project embeds as the host's, with the anchors the runtime places it at.</summary>
    /// <param name="format">The format the files are written in, read as the runtime reads it.</param>
    /// <param name="project">The project whose folder holds the host's files.</param>
    /// <param name="localizationFiles">The locale files this project owns, which the target attributed.</param>
    public static IReadOnlyList<PlacedLocaleFile> ResolveEmbedded(ILocaleFileFormat format, LoadedProject project, IReadOnlyCollection<string> localizationFiles)
        => Place(format, project, localizationFiles, static resourceName => resourceName is not null && IsHostResourceName(resourceName));

    // The build names an embedded host's file for its path in the folder, so both forms are placed by that path.
    private static List<PlacedLocaleFile> Place(ILocaleFileFormat format, LoadedProject project, IReadOnlyCollection<string> localizationFiles, Func<string?, bool> isInForm)
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
            if (!isInForm(resourceNames.GetValueOrDefault(fullPath))
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
