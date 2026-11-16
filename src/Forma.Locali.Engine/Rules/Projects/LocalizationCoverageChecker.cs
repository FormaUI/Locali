namespace Forma.Locali.Rules.Projects;

using System.Globalization;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

/// <summary>
/// Checks that a key written in source is answered in every culture its type is already localized in. A new key
/// added to a component whose entries exist otherwise passes every check while rendering in the source
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
        var seen = new HashSet<(string Key, string AssemblyName, string TypeName, string CultureName)>();

        foreach (var site in Ordered(context.Callsites))
        {
            // The runtime matches the key behind its escape, and so do the entries it read from the file.
            var key = LocalizationSchema.Unescape(site.Key.Text);

            foreach (var resource in site.ResourceTypes)
            {
                // A type's entries are its own assembly's, and a type no project of the run builds has none the run can read.
                if (context.GetOwnerLocalization(resource) is not { } owner)
                {
                    continue;
                }

                var typeName = ResourceTypeNames.GetName(resource);

                foreach (var culture in owner.Cultures)
                {
                    // A type with no entries in a culture is unlocalized there rather than under-localized.
                    var entries = owner.Read(typeName, culture);
                    if (entries.Count == 0
                        || IsAnswered(owner, localization, typeName, culture, key)
                        || !seen.Add((key, owner.AssemblyName, typeName, culture.Name)))
                    {
                        continue;
                    }

                    var filePath = owner.GetFile(entries[^1].FileName) is { } file
                        ? FormatFilePath(site.Key.Location.Path, file.Path)
                        : entries[^1].FileName;

                    context.Report(
                        Code,
                        site,
                        $"'{site.Key.Text}' has no entry in '{filePath}', which localizes '{typeName}' in '{culture.Name}', so it renders in the source language there.");
                }
            }
        }
    }

    // Every culture's file can carry the same name, so a file is named by where it sits from the callsite's folder.
    private static string FormatFilePath(string callsitePath, string filePath)
        => Path.GetDirectoryName(callsitePath) is { Length: > 0 } callsiteFolder
            ? Path.GetRelativePath(callsiteFolder, filePath)
            : filePath;

    // The runtime walks the culture up to its parents, so a regional file need only carry what differs, and an application lays its
    // own files for another assembly's type over that assembly's.
    private static bool IsAnswered(ProjectLocalization owner, ProjectLocalization lookupProject, string typeName, CultureInfo culture, string key)
    {
        for (var candidate = culture; candidate.Name.Length != 0; candidate = candidate.Parent)
        {
            if (owner.Read(typeName, candidate).Any(entry => entry.Key == key)
                || (lookupProject != owner && lookupProject.ReadApplication(owner.AssemblyName, typeName, candidate).Any(entry => entry.Key == key)))
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
