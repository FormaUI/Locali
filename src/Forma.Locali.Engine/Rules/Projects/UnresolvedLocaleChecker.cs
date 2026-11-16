namespace Forma.Locali.Rules.Projects;

using Forma.Locali.Diagnostics;
using Forma.Locali.Flow;
using Forma.Locali.Sources;

/// <summary>Checks that a lookup writing its key in a localized project reaches a type its locale is read for, so the key can be checked.</summary>
public sealed class UnresolvedLocaleChecker : IProjectRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.UnresolvedLocale;

    /// <inheritdoc />
    public void Check(ProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A project localized in no culture, by its own files or a pack, has no key to check, so nothing went unchecked.
        if (context.Localization is not { Cultures.Count: > 0 })
        {
            return;
        }

        foreach (var site in context.Callsites)
        {
            // A lookup reaching a type is checked, one whose hinted type member tells none is LOC0214's, and one no path was followed
            // from, found without a bound compilation or in code no instance runs, ended nowhere to name.
            if (site.Origins.Any(origin => !origin.IsApproximate)
                || site.Gaps.Any(gap => gap.Kind == LocaleGapKind.UnreadTypeMember)
                || site.Gaps.Count == 0 && site.Origins.Count == 0)
            {
                continue;
            }

            context.Report(
                Code,
                site,
                $"No path from the lookup reaches the type its locale is read for, ending at {Ending(site)}, so no rule could check whether a culture answers '{site.Key.Text}'.");
        }
    }

    private static string Ending(LocalizerLookup site)
        => site.Gaps is [var gap, ..]
            ? $"{Phrase(gap.Kind)}, '{gap.Detail}'"
            : $"alternatives the flow cannot pair with the lookup, '{string.Join("', '", site.Origins.Select(origin => origin.Type.Name).Distinct(StringComparer.Ordinal))}'";

    private static string Phrase(LocaleGapKind kind)
        => kind switch
        {
            LocaleGapKind.CompiledCode => "compiled code",
            LocaleGapKind.CustomFactory => "a factory the code builds itself",
            LocaleGapKind.NamedByString => "a locale named by a string",
            LocaleGapKind.Dispatch => "a call no one implementation answers that the flow can tell",
            LocaleGapKind.Reflection => "a value the framework sets",
            LocaleGapKind.ExternalCallers => "callers outside the run",
            LocaleGapKind.Local => "a local assigned more than once",
            LocaleGapKind.Write => "a member written where the flow cannot pair the write with the object read",
            LocaleGapKind.Cycle => "a path returning to itself",
            LocaleGapKind.Depth => "the depth the flow follows a path to",
            _ => "code the flow does not read",
        };
}
