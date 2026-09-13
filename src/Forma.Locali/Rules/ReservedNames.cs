namespace Forma.Locali.Rules;

using Forma.Framework.Localization.Files;

/// <summary>Reads a key for the shape of a reserved entry, by the runtime's own rule, and tells the names the runtime defines from the rest.</summary>
internal static class ReservedNames
{
    private static readonly HashSet<string> Defined = new(
        [LocalizationSchema.Marker, LocalizationSchema.SharedKey, LocalizationSchema.ToolsKey],
        StringComparer.Ordinal);

    /// <summary>Whether the runtime defines the name as one of its reserved entries.</summary>
    /// <param name="name">The key to read.</param>
    public static bool IsDefined(string name)
        => Defined.Contains(name);

    /// <summary>Whether the key is shaped like a reserved entry: a dollar sign, then one or more lowercase ASCII letters, and nothing else.</summary>
    /// <param name="name">The key to read.</param>
    public static bool IsShaped(string name)
        => LocalizationSchema.IsReservedShape(name);
}
