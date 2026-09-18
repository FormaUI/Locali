namespace Forma.Locali.Configuration;

/// <summary>A place in a config file that does not follow the shape the file is read for.</summary>
/// <param name="Offset">The byte the offending value starts at.</param>
/// <param name="Reason">What is wrong, in a sentence.</param>
public readonly record struct LocaliMalformedEntry(int Offset, string Reason);
