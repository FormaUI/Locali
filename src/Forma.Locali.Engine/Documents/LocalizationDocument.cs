namespace Forma.Locali.Documents;

using Forma.Locali.Diagnostics;

/// <summary>A localization file read as ordered entries, sections descended into, so the linter sees the order a dictionary would lose.</summary>
public sealed class LocalizationDocument
{
    /// <summary>A file's entries, as its format read them.</summary>
    /// <param name="path">The file the entries were read from.</param>
    /// <param name="map">Where each byte offset sits in the text.</param>
    /// <param name="entries">Every entry, in the order the file declares them.</param>
    public LocalizationDocument(string path, TextMap map, IReadOnlyList<LocalizationEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(entries);

        Path = path;
        Map = map;
        Entries = entries;
    }

    /// <summary>The file the entries were read from.</summary>
    public string Path { get; }

    /// <summary>Where each byte offset sits in the text.</summary>
    public TextMap Map { get; }

    /// <summary>Every entry, at the top level and inside sections, in the order the file declares them.</summary>
    public IReadOnlyList<LocalizationEntry> Entries { get; }

    /// <summary>Builds a diagnostic pointing at a byte offset in this file.</summary>
    /// <param name="offset">The byte offset the diagnostic is about.</param>
    /// <param name="code">The rule the diagnostic is reported under.</param>
    /// <param name="message">What is wrong, in a sentence.</param>
    /// <param name="severity">How much the diagnostic matters, which defaults to the rule's own severity.</param>
    public LintingDiagnostic Diagnose(int offset, LintingCode code, string message, LintingSeverity? severity = null)
    {
        ArgumentNullException.ThrowIfNull(code);

        var (line, column) = Map.Locate(offset);

        return new LintingDiagnostic(Path, line, column, severity ?? code.DefaultSeverity, code, message);
    }
}
