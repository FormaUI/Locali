namespace Forma.Locali.Sources;

using Forma.Locali.Flow;

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
    /// <summary>The types the lookup reads, each with where its path began; none where nothing named one.</summary>
    public IReadOnlyList<LocaleOrigin> Origins { get; init; } = [];

    /// <summary>Where a path from the lookup to its type ended before reaching one.</summary>
    public IReadOnlyList<LocaleGap> Gaps { get; init; } = [];

    /// <summary>The lookup's node, whose receiver the flow follows to its type once every project of the run is loaded, or null once its types are settled.</summary>
    public SyntaxNode? Receiver { get; init; }

    /// <summary>Whether the lookup goes through Microsoft's <c>IStringLocalizer</c>, which Forma's adapter answers from its type's entries, where only whether the key has an entry is checked, since a composite key is filled by <c>string.Format</c>.</summary>
    public bool UsesStringLocalizer { get; init; }
}
