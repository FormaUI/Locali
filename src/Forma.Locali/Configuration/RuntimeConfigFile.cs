namespace Forma.Locali.Configuration;

using System.Text;
using System.Text.Json;

using Forma.Framework.Localization.Files;
using Forma.Framework.Localization.Parser;

using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>The runtime's <c>.locale.json</c> file as the linter reads it, through the runtime's own <c>LocaleConfig</c> rules, with the offsets its diagnostics point at.</summary>
public sealed class RuntimeConfigFile
{
    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private RuntimeConfigFile(string path, TextMap map, IReadOnlyList<ConfigMalformedValue> malformedValues)
    {
        Path = path;
        Map = map;
        MalformedValues = malformedValues;
    }

    /// <summary>The file the entries were read from.</summary>
    public string Path { get; }

    /// <summary>Where each byte offset sits in the text.</summary>
    public TextMap Map { get; }

    /// <summary>Every value in the file that does not follow the shape the runtime reads it for.</summary>
    public IReadOnlyList<ConfigMalformedValue> MalformedValues { get; }

    /// <summary>Reads a file's entries, reporting the reason rather than throwing when it is not readable JSON.</summary>
    /// <param name="path">The file the bytes were read from.</param>
    /// <param name="content">The file's bytes.</param>
    /// <param name="file">The file, when reading succeeded.</param>
    /// <param name="error">Why reading failed, when it did.</param>
    public static bool TryRead(string path, byte[] content, out RuntimeConfigFile? file, out string? error)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(content);

        file = null;
        error = null;

        // A byte order mark would offset every position that follows it, so it leaves before anything is measured.
        var utf8 = content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF
            ? content[3..]
            : content;

        var malformedValues = new List<ConfigMalformedValue>();

        try
        {
            var reader = new Utf8JsonReader(utf8, ReaderOptions);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                error = "the file's top level is not a JSON object";

                return false;
            }

            ReadRoot(ref reader, malformedValues);
        }
        catch (JsonException exception)
        {
            error = exception.Message;

            return false;
        }

        file = new RuntimeConfigFile(path, new TextMap(utf8), malformedValues);

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

    // The runtime passes over a key it does not read, so a misspelled one steers nothing; here it is reported.
    private static void ReadRoot(ref Utf8JsonReader reader, List<ConfigMalformedValue> malformedValues)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var nameOffset = (int)reader.TokenStartIndex;
            var name = reader.GetString() ?? string.Empty;

            if (!reader.Read())
            {
                return;
            }

            // The runtime reads the file with duplicate keys disallowed, so a repeated key leaves the whole file unreadable to it.
            if (!names.Add(name))
            {
                malformedValues.Add(new ConfigMalformedValue(nameOffset, $"'{name}' is written more than once, so the runtime cannot read the file."));
                reader.Skip();

                continue;
            }

            switch (name)
            {
                case LocaleConfig.PathsKey:
                    ReadPaths(ref reader, malformedValues);
                    break;
                case LocaleConfig.RootNamespaceKey:
                case LocaleConfig.RootDirKey:
                case LocaleConfig.LocalesDirKey:
                    ReadSetting(ref reader, name, malformedValues);
                    break;
                default:
                    malformedValues.Add(new ConfigMalformedValue(nameOffset, $"'{name}' is not a key the runtime reads; it reads {FormatKeys()}."));
                    reader.Skip();
                    break;
            }
        }
    }

    // The runtime holds a value to one rule, which it states itself, so the two never refuse different files.
    private static void ReadSetting(ref Utf8JsonReader reader, string key, List<ConfigMalformedValue> malformedValues)
    {
        var offset = (int)reader.TokenStartIndex;
        if (reader.TokenType != JsonTokenType.String)
        {
            malformedValues.Add(new ConfigMalformedValue(offset, $"'{key}' is not text."));
            reader.Skip();

            return;
        }

        if (!LocaleConfig.Accepts(key, reader.GetString() ?? string.Empty, out var problem))
        {
            malformedValues.Add(new ConfigMalformedValue(offset, problem));
        }
    }

    // A path's diagnostic counts characters into the path, which become bytes past the value's opening quote; a value
    // carrying a JSON escape has no such correspondence, so its diagnostic points at the value.
    private static void ReadPaths(ref Utf8JsonReader reader, List<ConfigMalformedValue> malformedValues)
    {
        var listOffset = (int)reader.TokenStartIndex;
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            malformedValues.Add(new ConfigMalformedValue(
                listOffset,
                $"'{LocaleConfig.PathsKey}' is not a list of text; it lists the paths a locale file sits at."));
            reader.Skip();

            return;
        }

        var count = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            count++;
            var offset = (int)reader.TokenStartIndex;
            if (reader.TokenType != JsonTokenType.String)
            {
                malformedValues.Add(new ConfigMalformedValue(offset, "a path is not text."));
                reader.Skip();

                continue;
            }

            var text = reader.GetString() ?? string.Empty;
            if (LocalePathParser.Parse(text).Diagnostics is [var diagnostic, ..])
            {
                var place = reader.ValueIsEscaped
                    ? offset
                    : offset + 1 + Encoding.UTF8.GetByteCount(text.AsSpan(0, diagnostic.Span.Start));

                malformedValues.Add(new ConfigMalformedValue(place, LocalePath.FormatProblem(text, diagnostic)));
            }
        }

        if (count == 0)
        {
            malformedValues.Add(new ConfigMalformedValue(listOffset, $"'{LocaleConfig.PathsKey}' lists no path, which the runtime refuses."));
        }
    }

    private static string FormatKeys()
        => string.Join(", ", LocaleConfig.Keys.Select(key => $"'{key}'"));
}
