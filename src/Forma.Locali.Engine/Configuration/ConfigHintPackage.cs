namespace Forma.Locali.Configuration;

/// <summary>One item of a run's <c>hints.packages</c> list: a package whose <c>.locali-hints.json</c> the run reads.</summary>
/// <param name="Id">The package's identifier, as written.</param>
/// <param name="Offset">The byte the identifier's opening quote sits at.</param>
public readonly record struct ConfigHintPackage(string Id, int Offset);
