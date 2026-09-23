namespace Forma.Locali.Sources;

/// <summary>A locale pack's files, a folder inside it for each assembly they localize, resolved against the projects of the run.</summary>
/// <param name="Folders">The pack's top-level folders, ordered by the assembly name each spells.</param>
/// <param name="IsEveryAssemblyKnown">Whether the run named a solution every project of which compiled, so an assembly no project builds or references is built nowhere the solution reaches.</param>
public sealed record LocalePack(IReadOnlyList<LocalePackFolder> Folders, bool IsEveryAssemblyKnown);
