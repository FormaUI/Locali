namespace Forma.Locali.Configuration;

/// <summary>A run's <c>localeSources</c> entry: the sources it allows a locale file to come from, where the nearest file naming them wins.</summary>
/// <param name="Allowed">The sources named, combined.</param>
/// <param name="Offset">The byte the entry's opening bracket sits at.</param>
public readonly record struct ConfigLocaleSources(LocaleSources Allowed, int Offset);
