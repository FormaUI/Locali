namespace Forma.Locali.Rules.Files;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>Checks that a file gives a key the same text in every section that declares it, since each section's type shows its own.</summary>
public sealed class InconsistentLocalizationChecker : ILocalizationFileRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.InconsistentLocalization;

    /// <inheritdoc />
    public void Check(LocalizationFileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A section's last declaration is the one the runtime keeps, so it is the one compared.
        var entriesByKey = new Dictionary<string, Dictionary<string, LocalizationEntry>>(StringComparer.Ordinal);

        foreach (var entry in context.Document.Entries)
        {
            if (!entry.IsText || entry.Name == LocalizationSchema.Marker)
            {
                continue;
            }

            if (!entriesByKey.TryGetValue(entry.Name, out var entriesBySection))
            {
                entriesBySection = [with(StringComparer.Ordinal)];
                entriesByKey[entry.Name] = entriesBySection;
            }

            entriesBySection[entry.Section] = entry;
        }

        foreach (var entriesBySection in entriesByKey.Values)
        {
            Compare(context, entriesBySection.Values);
        }
    }

    private void Compare(LocalizationFileContext context, IEnumerable<LocalizationEntry> declarations)
    {
        LocalizationEntry? first = null;

        foreach (var entry in declarations)
        {
            if (first is not { } reference)
            {
                first = entry;

                continue;
            }

            var text = GetLocalizedText(entry);
            var referenceText = GetLocalizedText(reference);

            if (text == referenceText)
            {
                continue;
            }

            context.Report(
                Code,
                entry.NameOffset,
                $"The key '{entry.Name}' has the text '{text}' here and '{referenceText}' {Place(reference)}, so the same source text reads two ways at run time.");
        }
    }

    // A null value declares that the text is the key itself.
    private static string GetLocalizedText(LocalizationEntry entry)
        => entry.Value ?? entry.Name;

    private static string Place(LocalizationEntry entry)
        => entry.IsTopLevel ? "at the file's top level" : $"under '{entry.Section}'";
}
