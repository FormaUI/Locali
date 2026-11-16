namespace Forma.Locali.Sources;

using Microsoft.CodeAnalysis;

/// <summary>A localization lookup and the context it resolves in.</summary>
/// <param name="Key">The key the lookup reads.</param>
/// <param name="Arguments">How the lookup supplies the values its key's holes read.</param>
/// <param name="IsFormatString">Whether the lookup itself supplies the format text a <c>string.Format</c> call reads.</param>
public sealed record LocalizerLookup(
    LocalizationKeyReference Key,
    LocalizerArguments Arguments,
    bool IsFormatString)
{
    /// <summary>The types whose entries the lookup reads, one for each type of instance it is read through, or none where nothing bound it.</summary>
    public IReadOnlyList<INamedTypeSymbol> ResourceTypes { get; init; } = [];

    /// <summary>The properties naming a type the lookup reads whose getter has not told it: one its project holds as metadata alone until the run reads it, or one no reading tells.</summary>
    public IReadOnlyList<LocaleSourceProperty> UnreadProperties { get; init; } = [];
}
