namespace Forma.Locali.Sources;

/// <summary>The formatting arguments a lookup supplies.</summary>
/// <param name="Kind">How the lookup supplies the values its key's holes read.</param>
/// <param name="ValueCount">How many values the lookup supplies, or null where that count is not known.</param>
public sealed record LocalizerArguments(LocalizerArgumentKind Kind, int? ValueCount)
{
    /// <summary>A lookup that names a key and supplies no value.</summary>
    public static LocalizerArguments None { get; } = new(LocalizerArgumentKind.None, 0);

    /// <summary>A lookup whose overload could not be resolved, so what it supplies is not known.</summary>
    public static LocalizerArguments Unknown { get; } = new(LocalizerArgumentKind.Unknown, null);

    /// <summary>A lookup whose values pair to the key's distinct holes by position.</summary>
    /// <param name="count">How many values the lookup supplies.</param>
    public static LocalizerArguments Positional(int count)
        => new(LocalizerArgumentKind.Positional, count);
}
