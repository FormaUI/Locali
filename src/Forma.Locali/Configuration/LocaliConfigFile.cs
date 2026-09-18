namespace Forma.Locali.Configuration;

using System.Text.Json;

using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>A run's <c>.locali.json</c> file, read once as ordered entries with the offsets its diagnostics point at.</summary>
public sealed class LocaliConfigFile
{
    private const string IgnoreKey = "ignore";
    private const string ExcludeKey = "exclude";

    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private LocaliConfigFile(
        string path,
        TextMap map,
        IReadOnlyList<LocaliIgnoreEntry> ignoreEntries,
        IReadOnlyList<LocaliGlobEntry> excludeEntries,
        IReadOnlyList<LocaliMalformedEntry> malformed)
    {
        Path = path;
        Map = map;
        IgnoreEntries = ignoreEntries;
        ExcludeEntries = excludeEntries;
        Malformed = malformed;
    }

    /// <summary>The file the entries were read from.</summary>
    public string Path { get; }

    /// <summary>Where each byte offset sits in the text.</summary>
    public TextMap Map { get; }

    /// <summary>Every glob the file's <c>ignore</c> entry names, with the rules it leaves out for what it matches.</summary>
    public IReadOnlyList<LocaliIgnoreEntry> IgnoreEntries { get; }

    /// <summary>Every glob the file's <c>exclude</c> entry names.</summary>
    public IReadOnlyList<LocaliGlobEntry> ExcludeEntries { get; }

    /// <summary>Every place the file does not follow the shape it is read for.</summary>
    public IReadOnlyList<LocaliMalformedEntry> Malformed { get; }

    /// <summary>Reads a file's entries, reporting the reason rather than throwing when it is not readable JSON.</summary>
    /// <param name="path">The file the bytes were read from.</param>
    /// <param name="content">The file's bytes.</param>
    /// <param name="document">The document, when reading succeeded.</param>
    /// <param name="error">Why reading failed, when it did.</param>
    public static bool TryRead(string path, byte[] content, out LocaliConfigFile? document, out string? error)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(content);

        document = null;
        error = null;

        // A byte order mark would offset every position that follows it, so it leaves before anything is measured.
        var utf8 = content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF
            ? content[3..]
            : content;

        var ignore = new List<LocaliIgnoreEntry>();
        var exclude = new List<LocaliGlobEntry>();
        var malformed = new List<LocaliMalformedEntry>();

        try
        {
            var reader = new Utf8JsonReader(utf8, ReaderOptions);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                error = "the file's top level is not a JSON object";

                return false;
            }

            ReadRoot(ref reader, ignore, exclude, malformed);
        }
        catch (JsonException exception)
        {
            error = exception.Message;

            return false;
        }

        document = new LocaliConfigFile(path, new TextMap(utf8), ignore, exclude, malformed);

        return true;
    }

    /// <summary>Builds a diagnostic pointing at a byte offset in this file.</summary>
    /// <param name="offset">The byte offset the diagnostic is about.</param>
    /// <param name="code">The rule the diagnostic is reported under.</param>
    /// <param name="message">What is wrong, in a sentence.</param>
    public LintingDiagnostic Diagnose(int offset, LintingCode code, string message)
    {
        ArgumentNullException.ThrowIfNull(code);

        var (line, column) = Map.Locate(offset);

        return new LintingDiagnostic(Path, line, column, code.DefaultSeverity, code, message);
    }

    private static void ReadRoot(ref Utf8JsonReader reader, List<LocaliIgnoreEntry> ignore, List<LocaliGlobEntry> exclude, List<LocaliMalformedEntry> malformed)
    {
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var nameOffset = (int)reader.TokenStartIndex;
            var name = reader.GetString() ?? string.Empty;

            if (!reader.Read())
            {
                return;
            }

            switch (name)
            {
                case IgnoreKey:
                    ReadIgnore(ref reader, ignore, malformed);
                    break;
                case ExcludeKey:
                    ReadExclude(ref reader, exclude, malformed);
                    break;
                default:
                    malformed.Add(new LocaliMalformedEntry(nameOffset, $"'{name}' is not an entry this file defines; it reads {FormatKeys()}."));
                    reader.Skip();
                    break;
            }
        }
    }

    private static string FormatKeys()
        => string.Join(", ", new[] { IgnoreKey, ExcludeKey }.Select(key => $"'{key}'"));

    private static void ReadIgnore(ref Utf8JsonReader reader, List<LocaliIgnoreEntry> ignore, List<LocaliMalformedEntry> malformed)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            malformed.Add(new LocaliMalformedEntry((int)reader.TokenStartIndex, $"'{IgnoreKey}' is not an object; it pairs a glob with the rules it leaves out."));
            reader.Skip();

            return;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var globOffset = (int)reader.TokenStartIndex;
            var glob = reader.GetString() ?? string.Empty;

            if (!reader.Read())
            {
                return;
            }

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                malformed.Add(new LocaliMalformedEntry((int)reader.TokenStartIndex, $"'{glob}' is not a list of text; it lists rule identifiers."));
                reader.Skip();

                continue;
            }

            ignore.Add(new LocaliIgnoreEntry(glob, globOffset, ReadCodes(ref reader, malformed)));
        }
    }

    // An array is read through to its end either way, so a malformed item is skipped rather than abandoning the rest.
    private static List<LocaliCodeEntry> ReadCodes(ref Utf8JsonReader reader, List<LocaliMalformedEntry> malformed)
    {
        var codes = new List<LocaliCodeEntry>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                codes.Add(new LocaliCodeEntry(reader.GetString() ?? string.Empty, (int)reader.TokenStartIndex));
            }
            else
            {
                malformed.Add(new LocaliMalformedEntry((int)reader.TokenStartIndex, "a rule identifier is not text."));
                reader.Skip();
            }
        }

        return codes;
    }

    private static void ReadExclude(ref Utf8JsonReader reader, List<LocaliGlobEntry> exclude, List<LocaliMalformedEntry> malformed)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            malformed.Add(new LocaliMalformedEntry((int)reader.TokenStartIndex, $"'{ExcludeKey}' is not a list of text; it lists globs."));
            reader.Skip();

            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                exclude.Add(new LocaliGlobEntry(reader.GetString() ?? string.Empty, (int)reader.TokenStartIndex));
            }
            else
            {
                malformed.Add(new LocaliMalformedEntry((int)reader.TokenStartIndex, "a glob is not text."));
                reader.Skip();
            }
        }
    }
}
