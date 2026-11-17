namespace Forma.Locali.Projects;

/// <summary>A package a project compiles against, as restore laid it out: the folder it was restored into and the assemblies it ships.</summary>
/// <param name="Id">The package's identifier.</param>
/// <param name="Root">The folder the package was restored into.</param>
/// <param name="Assemblies">The name of each assembly the project compiles against from the package.</param>
public sealed record RestoredPackage(string Id, string Root, IReadOnlyCollection<string> Assemblies);
