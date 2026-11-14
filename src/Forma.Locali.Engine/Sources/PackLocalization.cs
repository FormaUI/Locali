namespace Forma.Locali.Sources;

/// <summary>A locale pack's files naming a project's assembly, with the pack read as the runtime reads it for the project's types.</summary>
/// <param name="Pack">The pack the files are in.</param>
/// <param name="Assembly">The project's assembly, with the pack's files whose names start with its name.</param>
/// <param name="Source">The pack as the runtime's walk reads it, laid out by the project's root namespace.</param>
public sealed record PackLocalization(LocalePack Pack, LocalePackAssembly Assembly, PackLocaleSource Source)
{
    /// <summary>The pack's readable culture-named files whose names start with the project's assembly name.</summary>
    public IReadOnlyList<ProjectLocaleFile> Files
        => Assembly.Files;

    /// <summary>Every file naming the assembly with the anchors the runtime places it at for the assembly.</summary>
    /// <param name="assemblyName">The name of the assembly the files name.</param>
    public IReadOnlyList<PlacedLocaleFile> GetPlacedFiles(string assemblyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyName);

        return [.. Files.Select(file => new PlacedLocaleFile(file.Path, file.Culture.Name, Source.GetAnchorNames(assemblyName, file)))];
    }
}
