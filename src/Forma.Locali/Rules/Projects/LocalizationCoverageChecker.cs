namespace Forma.Locali.Rules.Projects;

using System.Globalization;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

/// <summary>
/// Checks that a key written in source is answered in every culture its type is already translated for. A new key
/// added to a component whose translations exist otherwise passes every check while rendering in the source
/// language, because falling back to the key is what an absent entry is supposed to do.
/// </summary>
public sealed class LocalizationCoverageChecker : IProjectRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.MissingLocalization;

    /// <inheritdoc />
    public void Check(ProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Localization is not { } localization)
        {
            return;
        }

        // A key written at several sites is reported once per culture it is missing from, at the first site.
        var seen = new HashSet<(string Key, string TypeName, string CultureName)>();

        foreach (var site in Ordered(context.Callsites))
        {
            if (site.ResourceType is not { } resource)
            {
                continue;
            }

            var typeName = ResourceTypeNames.GetName(resource);

            // The runtime matches the key behind its escape, and so do the entries it read from the file.
            var key = LocalizationSchema.Unescape(site.Key.Text);

            foreach (var culture in localization.Cultures)
            {
                // A type with no translations in a culture is unlocalized there rather than under-translated.
                var entries = localization.Read(typeName, culture);
                if (entries.Count == 0 || IsAnswered(localization, typeName, culture, key) || !seen.Add((key, typeName, culture.Name)))
                {
                    continue;
                }

                var filePath = localization.Source.GetFile(entries[^1].FileName) is { } file
                    ? FormatFilePath(site.Key.Location.Path, file.Path)
                    : entries[^1].FileName;

                context.Report(
                    Code,
                    site,
                    $"'{site.Key.Text}' has no entry in '{filePath}', which translates '{typeName}' for '{culture.Name}', so it renders in the source language there.");
            }
        }
    }

    // Every culture's file can carry the same name, so a file is named by where it sits from the callsite's folder.
    private static string FormatFilePath(string callsitePath, string filePath)
        => Path.GetDirectoryName(callsitePath) is { Length: > 0 } callsiteFolder
            ? Path.GetRelativePath(callsiteFolder, filePath)
            : filePath;

    // The runtime walks the culture up to its parents, so a regional file need only carry what differs.
    private static bool IsAnswered(ProjectLocalization localization, string typeName, CultureInfo culture, string key)
    {
        for (var candidate = culture; candidate.Name.Length != 0; candidate = candidate.Parent)
        {
            if (localization.Read(typeName, candidate).Any(entry => entry.Key == key))
            {
                return true;
            }
        }

        return false;
    }

    // The walk that found the sites ran in parallel, so which site a key is reported at is settled here.
    private static IEnumerable<LocalizerLookup> Ordered(IReadOnlyList<LocalizerLookup> callsites)
        => callsites
            .OrderBy(site => site.Key.Location.Path, StringComparer.Ordinal)
            .ThenBy(site => site.Key.Location.StartLinePosition.Line)
            .ThenBy(site => site.Key.Location.StartLinePosition.Character);
}
