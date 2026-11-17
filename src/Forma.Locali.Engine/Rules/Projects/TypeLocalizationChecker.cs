namespace Forma.Locali.Rules.Projects;

using System.Globalization;

using Forma.Locali.Diagnostics;
using Forma.Locali.Flow;
using Forma.Locali.Sources;

using Microsoft.CodeAnalysis;

/// <summary>
/// Checks that a type lookups read has entries of its own in some culture its assembly's locale files carry. A new component
/// whose type has no section anywhere passes every check while rendering every key in the source language, because a type
/// with no entries reads as one left unlocalized on purpose.
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

                // A type with entries of its own in any culture is localized, and LOC0119 holds it to the culture families it leaves out.
                var typeName = ResourceTypeNames.GetName(origin.Type);
                if (HasOwnEntries(owner, typeName) || !seen.Add((owner.AssemblyName, typeName)))
                {
                    continue;
                }

                // In a culture any entry reaches the type in, a shared block's above it among them, the type is not left wholly in the source language.
                foreach (var culture in owner.Cultures.Where(culture => !HasEntries(owner, localization, typeName, culture)).OrderBy(culture => culture.Name, StringComparer.Ordinal))
                {
                    context.Report(
                        Code,
                        site,
                        $"'{typeName}', the type the lookup reads through {origin.Root}{HintClause(site, origin)}, has no entries in '{culture.Name}', a culture {owner.AssemblyName}'s locale files carry, so every key read for it renders in the source language there.");
                }
            }
        }
    }

    // LOC0119 reads the assembly's own files alone, so an application's files for the type leave it to this rule.
    private static bool HasOwnEntries(ProjectLocalization owner, string typeName)
        => owner.Cultures.Any(culture => owner.Read(typeName, culture).Any(entry => owner.IsOwn(typeName, entry)));

    // An application lays its own files for another assembly's type over that assembly's.
    private static bool HasEntries(ProjectLocalization owner, ProjectLocalization lookupProject, string typeName, CultureInfo culture)
        => WithParents(culture).Any(candidate =>
            owner.Read(typeName, candidate).Count > 0
            || (lookupProject != owner && lookupProject.ReadApplication(owner.AssemblyName, typeName, candidate).Count > 0));

    // The runtime walks a culture up to its parents for what it does not declare.
    private static IEnumerable<CultureInfo> WithParents(CultureInfo culture)
    {
        for (var candidate = culture; candidate.Name.Length != 0; candidate = candidate.Parent)
        {
            yield return candidate;
        }
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
