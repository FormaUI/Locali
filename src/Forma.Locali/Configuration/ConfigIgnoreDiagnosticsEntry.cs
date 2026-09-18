namespace Forma.Locali.Configuration;

/// <summary>One glob a run's <c>ignoreDiagnostics</c> entry names, with the rules it leaves out for a file the glob matches.</summary>
/// <param name="Glob">The glob as written.</param>
/// <param name="Offset">The byte the glob's opening quote sits at.</param>
/// <param name="Codes">The rule identifiers listed for this glob, in the order they are written.</param>
public readonly record struct ConfigIgnoreDiagnosticsEntry(string Glob, int Offset, IReadOnlyList<ConfigCodeEntry> Codes);
