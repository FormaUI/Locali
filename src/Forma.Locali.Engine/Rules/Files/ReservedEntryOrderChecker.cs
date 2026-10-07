namespace Forma.Locali.Rules.Files;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>Checks that the shared section precedes every translation and section.</summary>
public sealed class ReservedEntryOrderChecker : ILocalizationFileRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.ReservedEntryOutOfOrder;

    /// <inheritdoc />
    public void Check(LocalizationFileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var sawTopLevelItem = false;

        // The top-level items in order, a section's entries standing for the section once.
        foreach (var (name, offset) in GetTopLevelItems(context.Document))
        {
            if (name == LocalizationSchema.SharedKey)
            {
                if (sawTopLevelItem)
                {
                    context.Report(Code, offset, $"'{LocalizationSchema.SharedKey}' has to come before every translation and section, directly after '{LocalizationSchema.Marker}'.");
                }
            }
            else if (name != LocalizationSchema.Marker)
            {
                sawTopLevelItem = true;
            }
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
