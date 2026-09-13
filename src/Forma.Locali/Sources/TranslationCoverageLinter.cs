namespace Forma.Locali.Sources;

using System.Globalization;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;

/// <summary>
/// Checks that a key written in source is answered in every culture its type is already translated for. A new key
/// added to a component whose translations exist otherwise passes every check while rendering in the source
/// language, because falling back to the key is what an absent entry is supposed to do.
/// </summary>
public sealed class TranslationCoverageLinter : ITranslationCoverageLinter
{
    /// <inheritdoc />
    public IReadOnlyList<LintDiagnostic> Lint(ProjectTranslations translations, IReadOnlyList<LocalizerLookup> callSites)
    {
        ArgumentNullException.ThrowIfNull(translations);
        ArgumentNullException.ThrowIfNull(callSites);

        var diagnostics = new List<LintDiagnostic>();

        // A key written at several sites is reported once per culture it is missing from, at the first site.
        var seen = new HashSet<(string Key, string TypeName, string CultureName)>();

        foreach (var site in Ordered(callSites))
        {
            if (site.ResourceType is not { } resource)
            {
                continue;
            }

            var typeName = ResourceTypeNames.GetName(resource);

            // The runtime matches the key behind its escape, and so do the entries it read from the file.
            var key = LocalizationSchema.Unescape(site.Key.Text);

            foreach (var culture in translations.Cultures)
            {
                // A type with no translations in a culture is unlocalized there rather than under-translated.
                var entries = translations.Read(typeName, culture);
                if (entries.Count == 0 || IsAnswered(translations, typeName, culture, key) || !seen.Add((key, typeName, culture.Name)))
                {
                    continue;
                }

                var fileName = translations.Source.GetFile(entries[^1].FileName) is { } file ? Path.GetFileName(file.Path) : entries[^1].FileName;

                diagnostics.Add(new LintDiagnostic(
                    site.Key.Location.Path,
                    site.Key.Location.StartLinePosition.Line + 1,
                    site.Key.Location.StartLinePosition.Character + 1 + site.Key.TextOffset,
                    LintCodes.MissingTranslation.DefaultSeverity,
                    LintCodes.MissingTranslation,
                    $"'{site.Key.Text}' has no entry in '{fileName}', which translates '{typeName}' for '{culture.Name}', so it renders in the source language there."));
            }
        }

        return diagnostics;
    }

    // The runtime walks the culture up to its parents, so a regional file need only carry what differs.
    private static bool IsAnswered(ProjectTranslations translations, string typeName, CultureInfo culture, string key)
    {
        for (var candidate = culture; candidate.Name.Length != 0; candidate = candidate.Parent)
        {
            if (translations.Read(typeName, candidate).Any(entry => entry.Key == key))
            {
                return true;
            }
        }

        return false;
    }

    // The walk that found the sites ran in parallel, so which site a key is reported at is settled here.
    private static IEnumerable<LocalizerLookup> Ordered(IReadOnlyList<LocalizerLookup> callSites)
        => callSites
            .OrderBy(site => site.Key.Location.Path, StringComparer.Ordinal)
            .ThenBy(site => site.Key.Location.StartLinePosition.Line)
            .ThenBy(site => site.Key.Location.StartLinePosition.Character);
}
