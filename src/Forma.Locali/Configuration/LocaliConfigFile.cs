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
        IReadOnlyList<ConfigIgnoreDiagnostic> ignoreDiagnostics,
        IReadOnlyList<ConfigExcludeFile> excludeFiles,
        IReadOnlyList<ConfigMalformedValue> malformedValues)
    {
        Path = path;
        Map = map;
        IgnoreDiagnostics = ignoreDiagnostics;
        ExcludeFiles = excludeFiles;
        MalformedValues = malformedValues;
    }

    /// <summary>The file the entries were read from.</summary>
    public string Path { get; }

    /// <summary>Where each byte offset sits in the text.</summary>
    public TextMap Map { get; }

    /// <summary>Every glob the file's <c>ignoreDiagnostics</c> entry names, with the rules it leaves out for what it matches.</summary>
    public IReadOnlyList<ConfigIgnoreDiagnostic> IgnoreDiagnostics { get; }

    /// <summary>Every glob the file's <c>excludeFiles</c> entry names.</summary>
    public IReadOnlyList<ConfigExcludeFile> ExcludeFiles { get; }

    /// <summary>Every value in the file that does not follow the shape it is read for.</summary>
    public IReadOnlyList<ConfigMalformedValue> MalformedValues { get; }

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

        var ignoreDiagnostics = new List<ConfigIgnoreDiagnostic>();
        var excludeFiles = new List<ConfigExcludeFile>();
        var malformedValues = new List<ConfigMalformedValue>();

        try
        {
            var reader = new Utf8JsonReader(utf8, ReaderOptions);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                error = "the file's top level is not a JSON object";

                return false;
            }

            ReadRoot(ref reader, ignoreDiagnostics, excludeFiles, malformedValues);
        }
        catch (JsonException exception)
        {
            error = exception.Message;

            return false;
        }

        document = new LocaliConfigFile(path, new TextMap(utf8), ignoreDiagnostics, excludeFiles, malformedValues);

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

    private static void ReadRoot(ref Utf8JsonReader reader, List<ConfigIgnoreDiagnostic> ignoreDiagnostics, List<ConfigExcludeFile> excludeFiles, List<ConfigMalformedValue> malformedValues)
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
                    ReadIgnoreDiagnostics(ref reader, ignoreDiagnostics, malformedValues);
                    break;
                case ExcludeFilesKey:
                    ReadExcludeFiles(ref reader, excludeFiles, malformedValues);
                    break;
                default:
                    malformedValues.Add(new ConfigMalformedValue(nameOffset, $"'{name}' is not an entry this file defines; it reads {FormatKeys()}."));
                    reader.Skip();
                    break;
            }
        }
    }

    private static string FormatKeys()
        => string.Join(", ", new[] { IgnoreDiagnosticsKey, ExcludeFilesKey }.Select(key => $"'{key}'"));

    private static void ReadIgnoreDiagnostics(ref Utf8JsonReader reader, List<ConfigIgnoreDiagnostic> ignoreDiagnostics, List<ConfigMalformedValue> malformedValues)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            malformedValues.Add(new ConfigMalformedValue((int)reader.TokenStartIndex, $"'{IgnoreDiagnosticsKey}' is not an object; it pairs a glob with the rules it leaves out."));
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
                malformedValues.Add(new ConfigMalformedValue((int)reader.TokenStartIndex, $"'{glob}' is not a list of text; it lists rule identifiers."));
                reader.Skip();

                continue;
            }

            ignoreDiagnostics.Add(new ConfigIgnoreDiagnostic(glob, globOffset, ReadEntries(ref reader, malformedValues)));
        }
    }

    // An array is read through to its end either way, so a malformed item is skipped rather than abandoning the rest.
    private static List<ConfigIgnoreDiagnosticEntry> ReadEntries(ref Utf8JsonReader reader, List<ConfigMalformedValue> malformedValues)
    {
        var entries = new List<ConfigIgnoreDiagnosticEntry>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                entries.Add(new ConfigIgnoreDiagnosticEntry(reader.GetString() ?? string.Empty, (int)reader.TokenStartIndex));
            }
            else
            {
                malformedValues.Add(new ConfigMalformedValue((int)reader.TokenStartIndex, "a rule identifier is not text."));
                reader.Skip();
            }
        }

        return entries;
    }

    private static void ReadExcludeFiles(ref Utf8JsonReader reader, List<ConfigExcludeFile> excludeFiles, List<ConfigMalformedValue> malformedValues)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            malformedValues.Add(new ConfigMalformedValue((int)reader.TokenStartIndex, $"'{ExcludeFilesKey}' is not a list of text; it lists globs."));
            reader.Skip();

            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                excludeFiles.Add(new ConfigExcludeFile(reader.GetString() ?? string.Empty, (int)reader.TokenStartIndex));
            }
            else
            {
                malformedValues.Add(new ConfigMalformedValue((int)reader.TokenStartIndex, "a glob is not text."));
                reader.Skip();
            }
        }
    }
}
