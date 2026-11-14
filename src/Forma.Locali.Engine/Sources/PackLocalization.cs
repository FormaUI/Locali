namespace Forma.Locali.Sources;

/// <summary>A locale pack's files naming a project's assembly, with the pack read as the runtime reads it for the project's types.</summary>
/// <param name="Pack">The pack the files are in.</param>
/// <param name="Assembly">The project's assembly, with the pack's files whose names start with its name.</param>
/// <param name="Source">The pack as the runtime's walk reads it, laid out by the project's root namespace.</param>
public sealed record PackLocalization(LocalePack Pack, LocalePackAssembly Assembly, AssemblyNamedLocaleSource Source)
{
    /// <summary>The pack's readable culture-named files whose names start with the project's assembly name.</summary>
    public IReadOnlyList<ProjectLocaleFile> Files
        => Assembly.Files;
}
