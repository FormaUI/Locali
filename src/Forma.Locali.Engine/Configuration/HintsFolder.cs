namespace Forma.Locali.Configuration;

/// <summary>A folder a library's <c>.locali-hints.json</c> is read from, with the assemblies the library builds or ships, the only ones its hints may name.</summary>
/// <param name="Path">The folder: a project's, or the root a package was restored into.</param>
/// <param name="Assemblies">The names of the assemblies the library builds or ships.</param>
/// <param name="IsPackage">Whether the folder is a restored package's, whose file the run cannot change.</param>
public sealed record HintsFolder(string Path, IReadOnlyCollection<string> Assemblies, bool IsPackage);
