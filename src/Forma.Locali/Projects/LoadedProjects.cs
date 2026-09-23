namespace Forma.Locali.Projects;

/// <summary>The projects a run loaded, and whether they are every project its solution lists.</summary>
/// <param name="All">Every project the run loaded.</param>
/// <param name="IsWholeSolution">Whether the run named a solution, so the projects are every one it lists rather than whatever sat beneath a path.</param>
public sealed record LoadedProjects(IReadOnlyList<LoadedProject> All, bool IsWholeSolution);
