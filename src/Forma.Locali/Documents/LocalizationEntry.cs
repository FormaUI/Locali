namespace Forma.Locali.Documents;

/// <summary>One top-level entry of a localization file, with where it sits in the text.</summary>
/// <param name="Name">The key the entry is written under.</param>
/// <param name="Value">The entry's text, or null where the file writes JSON null.</param>
/// <param name="IsText">Whether the entry's value is a string or a null rather than some other shape.</param>
/// <param name="NameOffset">The byte the key's opening quote sits at.</param>
/// <param name="ValueOffset">The byte the value starts at.</param>
public readonly record struct LocalizationEntry(string Name, string? Value, bool IsText, int NameOffset, int ValueOffset);
