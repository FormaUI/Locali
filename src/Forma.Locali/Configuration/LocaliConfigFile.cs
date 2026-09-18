namespace Forma.Locali.Configuration;

using System.Text.Json;

using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>A run's <c>.locali.json</c> file, read once as ordered entries with the offsets its diagnostics point at.</summary>
public sealed class LocaliConfigFile
{
    private const string IgnoreDiagnosticsKey = "ignoreDiagnostics";
    private const string ExcludeFilesKey = "excludeFiles";

    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private LocaliConfigFile(
        string path,
        TextMap map,
        IReadOnlyList<ConfigIgnoreDiagnosticsEntry> ignoreDiagnostics,
        IReadOnlyList<ConfigExcludeFilesEntry> excludeFiles,
        IReadOnlyList<ConfigMalformedEntry> malformed)
    {
        Path = path;
        Map = map;
        IgnoreDiagnostics = ignoreDiagnostics;
        ExcludeFiles = excludeFiles;
        Malformed = malformed;
    }

    /// <summary>The file the entries were read from.</summary>
    public string Path { get; }

    /// <summary>Where each byte offset sits in the text.</summary>
    public TextMap Map { get; }

    /// <summary>Every glob the file's <c>ignoreDiagnostics</c> entry names, with the rules it leaves out for what it matches.</summary>
    public IReadOnlyList<ConfigIgnoreDiagnosticsEntry> IgnoreDiagnostics { get; }

    /// <summary>Every glob the file's <c>excludeFiles</c> entry names.</summary>
    public IReadOnlyList<ConfigExcludeFilesEntry> ExcludeFiles { get; }

    /// <summary>Every place the file does not follow the shape it is read for.</summary>
    public IReadOnlyList<ConfigMalformedEntry> Malformed { get; }

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

        var ignoreDiagnostics = new List<ConfigIgnoreDiagnosticsEntry>();
        var excludeFiles = new List<ConfigExcludeFilesEntry>();
        var malformed = new List<ConfigMalformedEntry>();

        try
        {
            var reader = new Utf8JsonReader(utf8, ReaderOptions);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                error = "the file's top level is not a JSON object";

                return false;
            }

            ReadRoot(ref reader, ignoreDiagnostics, excludeFiles, malformed);
        }
        catch (JsonException exception)
        {
            error = exception.Message;

            return false;
        }

        document = new LocaliConfigFile(path, new TextMap(utf8), ignoreDiagnostics, excludeFiles, malformed);

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

    private static void ReadRoot(ref Utf8JsonReader reader, List<ConfigIgnoreDiagnosticsEntry> ignoreDiagnostics, List<ConfigExcludeFilesEntry> excludeFiles, List<ConfigMalformedEntry> malformed)
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
                case IgnoreDiagnosticsKey:
                    ReadIgnoreDiagnostics(ref reader, ignoreDiagnostics, malformed);
                    break;
                case ExcludeFilesKey:
                    ReadExcludeFiles(ref reader, excludeFiles, malformed);
                    break;
                default:
                    malformed.Add(new ConfigMalformedEntry(nameOffset, $"'{name}' is not an entry this file defines; it reads {FormatKeys()}."));
                    reader.Skip();
                    break;
            }
        }
    }

    private static string FormatKeys()
        => string.Join(", ", new[] { IgnoreDiagnosticsKey, ExcludeFilesKey }.Select(key => $"'{key}'"));

    private static void ReadIgnoreDiagnostics(ref Utf8JsonReader reader, List<ConfigIgnoreDiagnosticsEntry> ignoreDiagnostics, List<ConfigMalformedEntry> malformed)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            malformed.Add(new ConfigMalformedEntry((int)reader.TokenStartIndex, $"'{IgnoreDiagnosticsKey}' is not an object; it pairs a glob with the rules it leaves out."));
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
                malformed.Add(new ConfigMalformedEntry((int)reader.TokenStartIndex, $"'{glob}' is not a list of text; it lists rule identifiers."));
                reader.Skip();

                continue;
            }

            ignoreDiagnostics.Add(new ConfigIgnoreDiagnosticsEntry(glob, globOffset, ReadCodes(ref reader, malformed)));
        }
    }

    // An array is read through to its end either way, so a malformed item is skipped rather than abandoning the rest.
    private static List<ConfigCodeEntry> ReadCodes(ref Utf8JsonReader reader, List<ConfigMalformedEntry> malformed)
    {
        var codes = new List<ConfigCodeEntry>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                codes.Add(new ConfigCodeEntry(reader.GetString() ?? string.Empty, (int)reader.TokenStartIndex));
            }
            else
            {
                malformed.Add(new ConfigMalformedEntry((int)reader.TokenStartIndex, "a rule identifier is not text."));
                reader.Skip();
            }
        }

        return codes;
    }

    private static void ReadExcludeFiles(ref Utf8JsonReader reader, List<ConfigExcludeFilesEntry> excludeFiles, List<ConfigMalformedEntry> malformed)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            malformed.Add(new ConfigMalformedEntry((int)reader.TokenStartIndex, $"'{ExcludeFilesKey}' is not a list of text; it lists globs."));
            reader.Skip();

            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                excludeFiles.Add(new ConfigExcludeFilesEntry(reader.GetString() ?? string.Empty, (int)reader.TokenStartIndex));
            }
            else
            {
                malformed.Add(new ConfigMalformedEntry((int)reader.TokenStartIndex, "a glob is not text."));
                reader.Skip();
            }
        }
    }
}
