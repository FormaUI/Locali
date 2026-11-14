namespace Forma.Locali.Sources;

using Forma.Locali.Projects;

/// <summary>A locale pack's files, resolved against the projects of the run by the assemblies their names start with.</summary>
/// <param name="Project">The pack's own project.</param>
/// <param name="Assemblies">Every assembly the run builds or references that is the longest a culture-named file of the pack names, ordered by name.</param>
/// <param name="FileNames">What each culture-named file of the pack is named, readable or not, by the file's full path.</param>
/// <param name="IsEveryAssemblyKnown">Whether the run named a solution every project of which compiled, so an assembly no project builds or references is built nowhere the solution reaches.</param>
public sealed record LocalePack(LoadedProject Project, IReadOnlyList<LocalePackAssembly> Assemblies, IReadOnlyDictionary<string, PackFileName> FileNames, bool IsEveryAssemblyKnown)
{
    /// <summary>Every readable culture-named file of the pack, whatever assembly its name starts with.</summary>
    public IReadOnlyList<ProjectLocaleFile> Files { get; } = [.. Assemblies.SelectMany(assembly => assembly.Files)];
}
