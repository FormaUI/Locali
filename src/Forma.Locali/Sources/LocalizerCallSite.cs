namespace Forma.Locali.Sources;

using Microsoft.CodeAnalysis;

/// <summary>A place in source where a localization key is looked up.</summary>
/// <param name="Key">The key the lookup reads.</param>
/// <param name="Location">Where the key sits, mapped back to the file a human wrote.</param>
/// <param name="KeyOffset">Where the key's text starts inside the literal.</param>
/// <param name="Arguments">How the lookup supplies the values its holes read.</param>
/// <param name="ValueCount">How many values the lookup supplies.</param>
/// <param name="ResourceType">The type whose translations the lookup reads, or null where nothing bound it.</param>
public sealed record LocalizerCallSite(
    string Key,
    FileLinePositionSpan Location,
    int KeyOffset,
    LocalizerArguments Arguments,
    int ValueCount,
    INamedTypeSymbol? ResourceType = null);
