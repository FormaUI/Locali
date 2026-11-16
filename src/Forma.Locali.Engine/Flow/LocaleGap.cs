namespace Forma.Locali.Flow;

/// <summary>Where a path from a lookup to its type ended before reaching one, and why.</summary>
/// <param name="Kind">Why the path ended.</param>
/// <param name="Detail">The place it ended, as a reader would recognize it in the code.</param>
public sealed record LocaleGap(LocaleGapKind Kind, string Detail);
