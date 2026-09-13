namespace Forma.Locali.Configuration;

/// <summary>One glob a run's <c>exclude</c> entry names.</summary>
/// <param name="Glob">The glob as written.</param>
/// <param name="Offset">The byte the glob's opening quote sits at.</param>
public readonly record struct LocaliGlobEntry(string Glob, int Offset);
