namespace Forma.Locali.Rules.Files;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>Checks that the tools entry directly follows the metadata entry and the shared section precedes every translation and section.</summary>
public sealed class ReservedEntryOrderChecker : ILocalizationFileRuleChecker
{
    /// <inheritdoc />
    public LintCode Code
        => LintCodes.ReservedEntryOutOfOrder;

    /// <inheritdoc />
    public void Check(LocalizationFileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var previous = default(string);
        var sawTranslation = false;

        // The top-level items in order, a section's entries standing for the section once.
        foreach (var (name, offset) in GetTopLevelItems(context.Document))
        {
            if (name == LocalizationSchema.ToolsKey)
            {
                if (previous != LocalizationSchema.Marker)
                {
                    context.Report(Code, offset, $"'{LocalizationSchema.ToolsKey}' has to come directly after '{LocalizationSchema.Marker}'.");
                }
            }
            else if (name == LocalizationSchema.SharedKey)
            {
                if (sawTranslation)
                {
                    context.Report(Code, offset, $"'{LocalizationSchema.SharedKey}' has to come before every translation and section, after '{LocalizationSchema.Marker}' and '{LocalizationSchema.ToolsKey}'.");
                }
            }
            else if (name != LocalizationSchema.Marker)
            {
                sawTranslation = true;
            }

            previous = name;
        }
    }

    private static IEnumerable<(string Name, int Offset)> GetTopLevelItems(LocalizationDocument document)
    {
        var lastSection = default(string);
        foreach (var entry in document.Entries)
        {
            if (entry.IsTopLevel)
            {
                lastSection = null;
                yield return (entry.Name, entry.NameOffset);
            }
            else
            {
                var section = entry.Section.Split('.')[0];
                if (section != lastSection)
                {
                    lastSection = section;
                    yield return (section, entry.NameOffset);
                }
            }
        }
    }
}
