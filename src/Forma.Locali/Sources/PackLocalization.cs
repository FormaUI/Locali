namespace Forma.Locali.Sources;

/// <summary>A locale pack's folder for a project, with the pack read as the runtime reads it for the project's types.</summary>
/// <param name="Pack">The pack the folder is in.</param>
/// <param name="Folder">The folder, named for the project's assembly.</param>
/// <param name="Source">The pack as the runtime's walk reads it, laid out by the project's root namespace.</param>
public sealed record PackLocalization(LocalePack Pack, LocalePackFolder Folder, PackLocaleSource Source)
{
    /// <summary>The pack's readable culture-named files beneath the folder.</summary>
    public IReadOnlyList<ProjectLocaleFile> Files
        => Folder.Files;

    /// <summary>Every file of the folder with the anchors the runtime places it at for the assembly.</summary>
    /// <param name="assemblyName">The name of the assembly the folder names.</param>
    public IReadOnlyList<PlacedLocaleFile> GetPlacedFiles(string assemblyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyName);

        return [.. Files.Select(file => new PlacedLocaleFile(file.Path, file.Culture.Name, Source.GetAnchorNames(assemblyName, file)))];
    }
}
