namespace Forma.Locali.Rules.Files;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>Checks that a text fills only the holes its key names.</summary>
public sealed class LocalizedTextHolesChecker : ILocalizationFileRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.UnknownLocalizedTextHole;

    /// <inheritdoc />
    public void Check(LocalizationFileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var entry in context.Document.Entries)
        {
            // The metadata entry declares what the file is, and a null value is the key left as written.
            if (!entry.IsText || entry.Value is not { } text || entry.Name == LocalizationSchema.Marker)
            {
                continue;
            }

            Compare(context, entry, text);
        }
    }

    private void Compare(LocalizationFileContext context, LocalizationEntry entry, string text)
    {
        var filled = HoleNames.Filled(text);

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
                    $"The text of '{entry.Name}' fills '{hole}', which the key does not name, so it renders as written.");
            }
        }
    }
}
