namespace Forma.Locali.Documents;

using System.Text.Json;

using Forma.Locali.Diagnostics;

/// <summary>A localization file read as ordered entries, sections descended into, so the linter sees the order a dictionary would lose.</summary>
public sealed class LocalizationDocument
{
    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private LocalizationDocument(string path, TextMap map, IReadOnlyList<LocalizationEntry> entries)
    {
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

    /// <summary>Reads a file's entries, reporting the reason rather than throwing when it is not readable JSON.</summary>
    public static bool TryRead(string path, byte[] content, out LocalizationDocument? document, out string? error)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);

        document = null;
        error = null;

        // A byte order mark would offset every position that follows it, so it leaves before anything is measured.
        var utf8 = content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF
            ? content[3..]
            : content;

        var entries = new List<LocalizationEntry>();

        try
        {
            var reader = new Utf8JsonReader(utf8, ReaderOptions);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                error = "the file's top level is not a JSON object";

                return false;
            }

            ReadEntries(ref reader, string.Empty, 0, entries);
        }
        catch (JsonException exception)
        {
            error = exception.Message;

            return false;
        }

        document = new LocalizationDocument(path, new TextMap(utf8), entries);

        return true;
    }

    // An object is a section holding the entries beneath it, named for the path of keys above them.
    private static void ReadEntries(ref Utf8JsonReader reader, string section, int property, List<LocalizationEntry> entries)
    {
        for (var position = 0; reader.Read() && reader.TokenType == JsonTokenType.PropertyName; position++)
        {
            var nameOffset = (int)reader.TokenStartIndex;
            var name = reader.GetString() ?? string.Empty;

            if (!reader.Read())
            {
                return;
            }

            // The top level numbers its own properties; a section's entries share the number of the property that opens it.
            var propertyIndex = section.Length == 0 ? position : property;

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                ReadEntries(ref reader, section.Length == 0 ? name : $"{section}.{name}", propertyIndex, entries);
                continue;
            }

            var valueOffset = (int)reader.TokenStartIndex;
            var isText = reader.TokenType is JsonTokenType.String or JsonTokenType.Null;
            var value = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            var items = default(IReadOnlyList<string>);

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                items = ReadItems(ref reader);
            }
            else if (!isText)
            {
                reader.Skip();
            }

            entries.Add(new LocalizationEntry(section, name, value, isText, items, nameOffset, valueOffset, propertyIndex));
        }
    }

    // An array is read through to its end either way; it is kept only when every item is a string.
    private static List<string>? ReadItems(ref Utf8JsonReader reader)
    {
        var items = new List<string>();
        var allText = true;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                items.Add(reader.GetString() ?? string.Empty);
            }
            else
            {
                allText = false;
                reader.Skip();
            }
        }

        return allText ? items : null;
    }

    /// <summary>Builds a diagnostic pointing at a byte offset in this file.</summary>
    /// <param name="offset">The byte offset the diagnostic is about.</param>
    /// <param name="code">The rule the diagnostic is reported under.</param>
    /// <param name="message">What is wrong, in a sentence.</param>
    /// <param name="severity">How much the diagnostic matters, which defaults to the rule's own severity.</param>
    public LintDiagnostic Diagnose(int offset, LintCode code, string message, LintSeverity? severity = null)
    {
        ArgumentNullException.ThrowIfNull(code);

        var (line, column) = Map.Locate(offset);

        return new LintDiagnostic(Path, line, column, severity ?? code.DefaultSeverity, code, message);
    }
}
