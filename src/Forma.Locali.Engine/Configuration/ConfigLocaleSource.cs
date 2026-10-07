namespace Forma.Locali.Configuration;

/// <summary>One line of a run's <c>localeSources</c> entry: a glob, with the sources a locale file it matches may come from; the last line matching a file speaks for it.</summary>
/// <param name="Glob">The glob as written.</param>
/// <param name="Offset">The byte the glob's opening quote sits at.</param>
/// <param name="Allowed">The sources named, combined.</param>
public readonly record struct ConfigLocaleSource(string Glob, int Offset, LocaleSources Allowed);
