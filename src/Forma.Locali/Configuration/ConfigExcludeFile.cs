namespace Forma.Locali.Configuration;

/// <summary>One line of a run's <c>excludeFiles</c> entry: a glob.</summary>
/// <param name="Glob">The glob as written.</param>
/// <param name="Offset">The byte the glob's opening quote sits at.</param>
public readonly record struct ConfigExcludeFile(string Glob, int Offset);
