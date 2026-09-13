namespace Forma.Locali.Sources;

using Microsoft.CodeAnalysis;

/// <summary>A localization lookup and the context it resolves in.</summary>
/// <param name="Key">The key the lookup reads.</param>
/// <param name="Arguments">How the lookup supplies the values its key's holes read.</param>
/// <param name="IsFormatString">Whether the lookup itself supplies the format text a <c>string.Format</c> call reads.</param>
/// <param name="ResourceType">The type whose translations the lookup reads, or null where nothing bound it.</param>
public sealed record LocalizerLookup(
    LocalizationKeyReference Key,
    LocalizerArguments Arguments,
    bool IsFormatString,
    INamedTypeSymbol? ResourceType = null);
