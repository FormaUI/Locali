namespace Forma.Locali.Documents;

/// <summary>One entry of a localization file, with the section it sits in and where it sits in the text.</summary>
/// <param name="Section">The sections above the entry joined by dots, or empty for an entry at the file's top level.</param>
/// <param name="Name">The key the entry is written under.</param>
/// <param name="Value">The entry's text, or null where the file writes JSON null or some other shape.</param>
/// <param name="IsText">Whether the entry's value is a string or a null rather than some other shape.</param>
/// <param name="Items">The strings of an array value, in order, or null where the value is not an array of strings.</param>
/// <param name="NameOffset">The byte the key's opening quote sits at.</param>
/// <param name="ValueOffset">The byte the value starts at.</param>
/// <param name="PropertyIndex">The position, counted from zero, of the top-level property the entry sits under.</param>
public readonly record struct LocalizationEntry(string Section, string Name, string? Value, bool IsText, IReadOnlyList<string>? Items, int NameOffset, int ValueOffset, int PropertyIndex)
{
    /// <summary>Whether the entry sits at the file's top level rather than inside a section.</summary>
    public bool IsTopLevel
        => Section.Length == 0;
}
