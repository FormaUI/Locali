namespace Forma.Locali.Rules.Projects;

using System.Globalization;

using Forma.Locali.Diagnostics;
using Forma.Locali.Flow;
using Forma.Locali.Sources;

using Microsoft.CodeAnalysis;

/// <summary>
/// Checks that a type lookups read is localized in some culture its assembly's locale files carry. A new component whose
/// type has no section anywhere passes every check while rendering every key in the source language, because a type with
/// no entries reads as one left unlocalized on purpose.
/// </summary>
public sealed class TypeLocalizationChecker : IProjectRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.UnlocalizedType;

    /// <inheritdoc />
    public void Check(ProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Localization is not { } localization)
        {
            return;
        }

        // A type read at several sites is reported once per culture, at the first site.
        var seen = new HashSet<(string AssemblyName, string TypeName)>();

        foreach (var site in Ordered(context.Callsites))
        {
            // An origin reached through alternatives the flow could not pair with the lookup may not be the type it reads.
            foreach (var origin in site.Origins.Where(origin => !origin.IsApproximate).DistinctBy(origin => origin.Type, SymbolEqualityComparer.Default))
            {
                // A type's entries are its own assembly's, and a type no project of the run builds has none the run can read.
                if (context.GetOwnerLocalization(origin.Type) is not { } owner)
                {
                    continue;
                }

                // A type with entries in any culture is localized, and LOC0207 and LOC0119 hold it to the cultures it leaves out.
                var typeName = ResourceTypeNames.GetName(origin.Type);
                if (owner.Cultures.Any(culture => HasEntries(owner, localization, typeName, culture)) || !seen.Add((owner.AssemblyName, typeName)))
                {
                    continue;
                }

                foreach (var culture in owner.Cultures.OrderBy(culture => culture.Name, StringComparer.Ordinal))
                {
                    context.Report(
                        Code,
                        site,
                        $"'{typeName}', the type the lookup reads through {origin.Root}{HintClause(site, origin)}, has no entries in '{culture.Name}', a culture {owner.AssemblyName}'s locale files carry, so every key read for it renders in the source language there.");
                }
            }
        }
    }

    // The runtime walks the culture up to its parents, and an application lays its own files for another assembly's type over that
    // assembly's.
    private static bool HasEntries(ProjectLocalization owner, ProjectLocalization lookupProject, string typeName, CultureInfo culture)
    {
        for (var candidate = culture; candidate.Name.Length != 0; candidate = candidate.Parent)
        {
            if (owner.Read(typeName, candidate).Count > 0
                || (lookupProject != owner && lookupProject.ReadApplication(owner.AssemblyName, typeName, candidate).Count > 0))
            {
                return true;
            }
        }

        return false;
    }

    // A type only a hint supplied names that hint, so a finding that rests on an assumption can be traced to it.
    private static string HintClause(LocalizerLookup site, LocaleOrigin origin)
        => origin is { Evidence: LocaleEvidence.Hint, Hint: { } hint }
            ? $" by the hint at '{FormatFilePath(site.Key.Location.Path, hint.Path)}:{hint.Line}'"
            : string.Empty;

    private static string FormatFilePath(string callsitePath, string filePath)
        => Path.GetDirectoryName(callsitePath) is { Length: > 0 } callsiteFolder
            ? Path.GetRelativePath(callsiteFolder, filePath)
            : filePath;

    // The walk that found the sites ran in parallel, so which site a type is reported at is settled here.
    private static IEnumerable<LocalizerLookup> Ordered(IReadOnlyList<LocalizerLookup> callsites)
        => callsites
            .OrderBy(site => site.Key.Location.Path, StringComparer.Ordinal)
            .ThenBy(site => site.Key.Location.StartLinePosition.Line)
            .ThenBy(site => site.Key.Location.StartLinePosition.Character);
}
