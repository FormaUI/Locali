namespace Forma.Locali.Configuration;

/// <summary>One rule an <c>ignoreDiagnostics</c> line leaves out.</summary>
/// <param name="Id">The identifier as written.</param>
/// <param name="Offset">The byte the identifier's opening quote sits at.</param>
public readonly record struct ConfigIgnoreDiagnosticEntry(string Id, int Offset);
