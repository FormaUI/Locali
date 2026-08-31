namespace Forma.Locali.Sources;

using Microsoft.CodeAnalysis;

/// <summary>The localization lookups one syntax tree writes.</summary>
/// <param name="Sites">The lookups whose key a rule can read.</param>
/// <param name="UnreadableKeys">Where a lookup writes its key as an expression no rule can read.</param>
/// <param name="InterpolatedKeys">Where a lookup writes its key as an interpolated string with holes.</param>
public sealed record LocalizerLookups(
    IReadOnlyList<LocalizerCallSite> Sites,
    IReadOnlyList<FileLinePositionSpan> UnreadableKeys,
    IReadOnlyList<FileLinePositionSpan> InterpolatedKeys)
{
    /// <summary>A tree that looks nothing up.</summary>
    public static LocalizerLookups Empty { get; } = new([], [], []);
}
