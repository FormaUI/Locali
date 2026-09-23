namespace Forma.Locali.Sources;

using Forma.Locali.Projects;

/// <summary>A top-level folder of a locale pack, named for the assembly whose types its files localize.</summary>
/// <param name="AssemblyName">The assembly's name, which the folder spells.</param>
/// <param name="Files">The pack's readable culture-named files beneath the folder, each with the name the runtime reads it by.</param>
/// <param name="Target">The project in the run that builds the assembly, or null when none does.</param>
/// <param name="IsReferenced">Whether a project in the run references an assembly of the name.</param>
public sealed record LocalePackFolder(string AssemblyName, IReadOnlyList<ProjectLocaleFile> Files, LoadedProject? Target, bool IsReferenced);
