namespace Forma.Locali.Sources;

using Forma.Locali.Projects;

/// <summary>A locale pack's files, a folder inside it for each assembly they localize, resolved against the projects of the run.</summary>
/// <param name="Project">The pack's own project.</param>
/// <param name="Folders">The pack's top-level folders, ordered by the assembly name each spells.</param>
/// <param name="IsEveryAssemblyKnown">Whether the run named a solution every project of which compiled, so an assembly no project builds or references is built nowhere the solution reaches.</param>
public sealed record LocalePack(LoadedProject Project, IReadOnlyList<LocalePackFolder> Folders, bool IsEveryAssemblyKnown)
{
    /// <summary>Every readable culture-named file of the pack, whatever assembly its folder names.</summary>
    public IReadOnlyList<ProjectLocaleFile> Files { get; } = [.. Folders.SelectMany(folder => folder.Files)];
}
