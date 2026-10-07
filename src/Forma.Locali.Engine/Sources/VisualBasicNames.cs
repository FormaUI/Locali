namespace Forma.Locali.Sources;

/// <summary>Compares names the way Visual Basic reads them, whatever their casing.</summary>
internal static class VisualBasicNames
{
    /// <summary>The comparer a Visual Basic name is looked up with.</summary>
    public static StringComparer Comparer { get; } = StringComparer.OrdinalIgnoreCase;

    /// <summary>Whether two names are the same name to Visual Basic.</summary>
    /// <param name="left">One name.</param>
    /// <param name="right">The other.</param>
    public static bool Equals(string? left, string? right)
        => Comparer.Equals(left, right);
}
