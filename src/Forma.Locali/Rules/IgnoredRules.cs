namespace Forma.Locali.Rules;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>The rules a file's tools entry lists under this linter's ignore entry.</summary>
/// <param name="Known">The rules listed that this linter defines.</param>
/// <param name="Unknown">The names that match no rule.</param>
public sealed record IgnoredRules(IReadOnlyList<LintCode> Known, IReadOnlyList<string> Unknown)
{
    /// <summary>No rules, which a file without an ignore entry is left out of.</summary>
    public static IgnoredRules None { get; } = new([], []);

    /// <summary>Reads the ignore entry's items, one rule identifier each.</summary>
    /// <param name="identifiers">The entry's items.</param>
    public static IgnoredRules Parse(IEnumerable<string> identifiers)
    {
        ArgumentNullException.ThrowIfNull(identifiers);

        var known = new List<LintCode>();
        var unknown = new List<string>();

        foreach (var identifier in identifiers)
        {
            if (LintCodes.All.FirstOrDefault(code => code.Id == identifier) is { } code)
            {
                known.Add(code);
            }
            else
            {
                unknown.Add(identifier);
            }
        }

        return new IgnoredRules(known, unknown);
    }

    /// <summary>The rules a file's ignore entry lists, or none when the file carries no such entry or one that is not a list of text.</summary>
    /// <param name="document">The file.</param>
    public static IgnoredRules Read(LocalizationDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var entry = document.Entries.FirstOrDefault(entry => entry.Section == LocalizationSchema.ToolsKey && entry.Name == ToolEntries.Ignore);

        return entry.Items is { } items ? Parse(items) : None;
    }

    /// <summary>Whether the file leaves a rule out; the rule over the tools entry itself is never left out.</summary>
    /// <param name="code">The rule.</param>
    public bool Ignores(LintCode code)
        => code != LintCodes.MalformedTools && Known.Contains(code);
}
