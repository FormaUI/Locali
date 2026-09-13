namespace Forma.Locali.Rules.Files;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>Checks that a translation fills only the holes its key names.</summary>
public sealed class TranslationHolesChecker : ILocalizationFileRuleChecker
{
    /// <inheritdoc />
    public LintCode Code
        => LintCodes.UnknownTranslationHole;

    /// <inheritdoc />
    public void Check(LocalizationFileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var entry in context.Document.Entries)
        {
            // The metadata entry and a tool's entries are not translations, and a null value is the key left as written.
            if (!entry.IsText || entry.IsInsideToolsSection || entry.Value is not { } translation || entry.Name == LocalizationSchema.Marker)
            {
                continue;
            }

            Compare(context, entry, translation);
        }
    }

    private void Compare(LocalizationFileContext context, LocalizationEntry entry, string translation)
    {
        var filled = HoleNames.Filled(translation);

        if (filled.Count == 0)
        {
            return;
        }

        var named = HoleNames.Filled(entry.Name);

        foreach (var hole in filled)
        {
            if (!named.Contains(hole, StringComparer.Ordinal))
            {
                context.Report(
                    Code,
                    entry.ValueOffset,
                    $"The translation of '{entry.Name}' fills '{hole}', which the key does not name, so it renders as written.");
            }
        }
    }
}
