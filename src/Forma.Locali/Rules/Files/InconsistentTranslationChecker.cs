namespace Forma.Locali.Rules.Files;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>Checks that a file translates a key the same way in every section that declares it, since each section's type shows its own.</summary>
public sealed class InconsistentTranslationChecker : ILocalizationFileRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.InconsistentTranslation;

    /// <inheritdoc />
    public void Check(LocalizationFileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A section's last declaration is the one the runtime keeps, so it is the one compared.
        var translationsByKey = new Dictionary<string, Dictionary<string, LocalizationEntry>>(StringComparer.Ordinal);

        foreach (var entry in context.Document.Entries)
        {
            if (!entry.IsText || entry.Name == LocalizationSchema.Marker)
            {
                continue;
            }

            if (!translationsByKey.TryGetValue(entry.Name, out var translationsBySection))
            {
                translationsBySection = new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);
                translationsByKey[entry.Name] = translationsBySection;
            }

            translationsBySection[entry.Section] = entry;
        }

        foreach (var translationsBySection in translationsByKey.Values)
        {
            Compare(context, translationsBySection.Values);
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

            var translation = Translation(entry);
            var referenceTranslation = Translation(reference);

            if (translation == referenceTranslation)
            {
                continue;
            }

            context.Report(
                Code,
                entry.NameOffset,
                $"The key '{entry.Name}' is translated as '{translation}' here and as '{referenceTranslation}' {Place(reference)}, so the same English text reads two ways at run time.");
        }
    }

    // A null value declares that the translation is the key text itself.
    private static string Translation(LocalizationEntry entry)
        => entry.Value ?? entry.Name;

    private static string Place(LocalizationEntry entry)
        => entry.IsTopLevel ? "at the file's top level" : $"under '{entry.Section}'";
}
