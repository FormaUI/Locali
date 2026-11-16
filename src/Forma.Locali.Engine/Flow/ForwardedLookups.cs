namespace Forma.Locali.Flow;

using Forma.Locali.Sources;

/// <summary>The lookups keys passed through parameters make where a call writes them as text, and the keys no path writes so throughout.</summary>
/// <param name="ByProject">The lookups each call makes, by the path of the project holding the call.</param>
/// <param name="Unforwarded">The keys some call writes otherwise, or that no call of the run writes at all.</param>
public sealed record ForwardedLookups(IReadOnlyDictionary<string, IReadOnlyList<LocalizerLookup>> ByProject, IReadOnlyList<ForwardedKey> Unforwarded)
{
    /// <summary>No key forwarded at all.</summary>
    public static ForwardedLookups None { get; } = new(new Dictionary<string, IReadOnlyList<LocalizerLookup>>(), []);
}
