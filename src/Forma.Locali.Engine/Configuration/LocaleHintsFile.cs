namespace Forma.Locali.Configuration;

using System.Text.Json;

using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>A library's <c>.locali-hints.json</c>, at its project's root and at its package's, read as ordered entries with the offsets its diagnostics point at.</summary>
public sealed class LocaleHintsFile
{
    /// <summary>The name a library's hints file is read under, at its project's root and at its package's.</summary>
    public const string FileName = ".locali-hints.json";

    internal const string LocaleMembersKey = "localeMembers";

    private const string AssemblyKey = "assembly";
    private const string TypeKey = "type";
    private const string MemberKey = "member";
    private const string TypeMemberKey = "typeMember";

    private LocaleHintsFile(string path, TextMap map, IReadOnlyList<ConfigLocaleMember> localeMembers, IReadOnlyList<ConfigMalformedValue> malformedValues)
    {
        Path = path;
        Map = map;
        LocaleMembers = localeMembers;
        MalformedValues = malformedValues;
    }

    /// <summary>The file the entries were read from.</summary>
    public string Path { get; }

    /// <summary>Where each byte offset sits in the text.</summary>
    public TextMap Map { get; }

    /// <summary>Every item of the file's <c>localeMembers</c> entry, in the order written.</summary>
    public IReadOnlyList<ConfigLocaleMember> LocaleMembers { get; }

    /// <summary>Every value in the file that does not follow the shape it is read for.</summary>
    public IReadOnlyList<ConfigMalformedValue> MalformedValues { get; }

    /// <summary>Reads a file's entries, reporting the reason rather than throwing when it is not readable JSON.</summary>
    /// <param name="path">The file the bytes were read from.</param>
    /// <param name="content">The file's bytes.</param>
    /// <param name="document">The document, when reading succeeded.</param>
    /// <param name="error">Why reading failed, when it did.</param>
    public static bool TryRead(string path, byte[] content, out LocaleHintsFile? document, out string? error)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(content);

        document = null;
        error = null;

        var utf8 = LinterConfigFile.WithoutByteOrderMark(content);
        var localeMembers = new List<ConfigLocaleMember>();
        var malformedValues = new List<ConfigMalformedValue>();

        try
        {
            var reader = new Utf8JsonReader(utf8, LinterConfigFile.ReaderOptions);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                error = "the file's top level is not a JSON object";

                return false;
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var nameOffset = (int)reader.TokenStartIndex;
                var name = reader.GetString() ?? string.Empty;

                if (!reader.Read())
                {
                    break;
                }

                if (name == LocaleMembersKey)
                {
                    ReadLocaleMembers(ref reader, localeMembers, malformedValues);
                }
                else
                {
                    malformedValues.Add(new ConfigMalformedValue(nameOffset, $"'{name}' is not an entry a hints file defines; it reads '{LocaleMembersKey}'."));
                    reader.Skip();
                }
            }
        }
        catch (JsonException exception)
        {
            error = exception.Message;

            return false;
        }

        document = new LocaleHintsFile(path, new TextMap(utf8), localeMembers, malformedValues);

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

    // An item names all four parts as text, and one missing a part or holding another is reported without stopping the list.
    internal static void ReadLocaleMembers(ref Utf8JsonReader reader, List<ConfigLocaleMember> localeMembers, List<ConfigMalformedValue> malformedValues)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            malformedValues.Add(new ConfigMalformedValue((int)reader.TokenStartIndex, $"'{LocaleMembersKey}' is not a list; it lists members, each with its '{AssemblyKey}', '{TypeKey}', '{MemberKey}' and '{TypeMemberKey}'."));
            reader.Skip();

            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var itemOffset = (int)reader.TokenStartIndex;
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                malformedValues.Add(new ConfigMalformedValue(itemOffset, $"a member is not an object; it names its '{AssemblyKey}', '{TypeKey}', '{MemberKey}' and '{TypeMemberKey}'."));
                reader.Skip();
                continue;
            }

            var parts = new Dictionary<string, string>(StringComparer.Ordinal);
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var partOffset = (int)reader.TokenStartIndex;
                var part = reader.GetString() ?? string.Empty;

                if (!reader.Read())
                {
                    return;
                }

                if (part is not (AssemblyKey or TypeKey or MemberKey or TypeMemberKey))
                {
                    malformedValues.Add(new ConfigMalformedValue(partOffset, $"'{part}' is not a part a member names; it names '{AssemblyKey}', '{TypeKey}', '{MemberKey}' and '{TypeMemberKey}'."));
                    reader.Skip();
                }
                else if (reader.TokenType != JsonTokenType.String || reader.GetString() is not { Length: > 0 } value)
                {
                    malformedValues.Add(new ConfigMalformedValue((int)reader.TokenStartIndex, $"'{part}' is not text naming something."));
                    reader.Skip();
                }
                else
                {
                    parts[part] = value;
                }
            }

            string[] missing = [.. new[] { AssemblyKey, TypeKey, MemberKey, TypeMemberKey }.Where(part => !parts.ContainsKey(part))];
            if (missing.Length > 0)
            {
                malformedValues.Add(new ConfigMalformedValue(itemOffset, $"a member names no {string.Join(" or ", missing.Select(part => $"'{part}'"))}, so it relates nothing."));
                continue;
            }

            localeMembers.Add(new ConfigLocaleMember(parts[AssemblyKey], parts[TypeKey], parts[MemberKey], parts[TypeMemberKey], itemOffset));
        }
    }
}
