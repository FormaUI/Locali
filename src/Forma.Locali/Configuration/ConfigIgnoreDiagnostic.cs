namespace Forma.Locali.Configuration;

/// <summary>One line of a run's <c>ignoreDiagnostics</c> entry: a glob, with the rules it leaves out for a file the glob matches.</summary>
/// <param name="Glob">The glob as written.</param>
/// <param name="Offset">The byte the glob's opening quote sits at.</param>
/// <param name="Entries">The rules listed for this glob, in the order they are written.</param>
public readonly record struct ConfigIgnoreDiagnostic(string Glob, int Offset, IReadOnlyList<ConfigIgnoreDiagnosticEntry> Entries);
