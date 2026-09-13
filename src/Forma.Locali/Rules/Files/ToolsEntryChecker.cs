namespace Forma.Locali.Rules.Files;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>Checks that the tools entry, where a file carries one, is an object at the top level and that what it holds for this linter is an ignore list naming rules this linter defines.</summary>
public sealed class ToolsEntryChecker : ILocalizationFileRuleChecker
{
    private static readonly string OwnPrefix = ToolEntries.Tool + '.';

    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.MalformedTools;

    /// <inheritdoc />
    public void Check(LocalizationFileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var misplaced = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in context.Document.Entries)
        {
            if (entry.Name == LocalizationSchema.ToolsKey)
            {
                context.Report(Code, entry.NameOffset, entry.IsTopLevel
                    ? $"'{LocalizationSchema.ToolsKey}' is not an object; it holds each tool's entries under keys naming the tool, '{ToolEntries.Ignore}' among them."
                    : $"'{LocalizationSchema.ToolsKey}' sits under '{entry.Section}'; it belongs at the file's top level, directly after '{LocalizationSchema.Marker}'.");

                continue;
            }

            var sections = entry.Section.Split('.');
            var index = Array.IndexOf(sections, LocalizationSchema.ToolsKey);
            if (index > 0)
            {
                var section = string.Join('.', sections[..index]);
                if (misplaced.Add(section))
                {
                    context.Report(Code, entry.NameOffset, $"'{LocalizationSchema.ToolsKey}' sits under '{section}'; it belongs at the file's top level, directly after '{LocalizationSchema.Marker}'.");
                }
            }
            else if (index == 0)
            {
                CheckToolEntry(context, entry, sections);
            }
        }
    }

    // What sits under the tools entry is this linter's to judge only under its own name; another tool's entries are left to it.
    private static void CheckToolEntry(LocalizationFileContext context, LocalizationEntry entry, string[] sections)
    {
        if (sections.Length > 1)
        {
            if (sections[1] == ToolEntries.Tool || sections[1].StartsWith(OwnPrefix, StringComparison.Ordinal))
            {
                context.Report(LintingCodes.MalformedTools, entry.NameOffset, $"'{sections[1]}' holds an object; this linter reads its entries directly under '{LocalizationSchema.ToolsKey}', as '{ToolEntries.Ignore}'.");
            }

            return;
        }

        if (entry.Name == ToolEntries.Ignore)
        {
            if (entry.Items is not { } items)
            {
                context.Report(LintingCodes.MalformedTools, entry.ValueOffset, $"'{ToolEntries.Ignore}' is not a list of text; it lists rule identifiers.");

                return;
            }

            foreach (var unknown in IgnoredRules.Parse(items).Unknown)
            {
                context.Report(LintingCodes.MalformedTools, entry.ValueOffset, $"'{ToolEntries.Ignore}' names '{unknown}', which is not a rule this linter defines.");
            }
        }
        else if (entry.Name.StartsWith(OwnPrefix, StringComparison.Ordinal))
        {
            context.Report(LintingCodes.MalformedTools, entry.NameOffset, $"'{entry.Name}' is not an entry this linter defines; it reads '{ToolEntries.Ignore}'.");
        }
    }
}
