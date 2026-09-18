namespace Forma.Locali.Configuration;

/// <summary>One rule identifier an <c>ignoreDiagnostics</c> entry names.</summary>
/// <param name="Id">The identifier as written.</param>
/// <param name="Offset">The byte the identifier's opening quote sits at.</param>
public readonly record struct ConfigCodeEntry(string Id, int Offset);
