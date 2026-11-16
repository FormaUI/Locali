namespace Forma.Locali.Rules.Projects;

using Forma.Locali.Diagnostics;
using Forma.Locali.Flow;

/// <summary>Checks that the type a lookup through a member carrying <c>[LocaleSource]</c> reads could be told from the property that attribute names.</summary>
public sealed class UnreadLocaleSourceChecker : IProjectRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.UnreadLocaleSource;

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
            foreach (var gap in site.Gaps.Where(gap => gap.Kind == LocaleGapKind.UnreadTypeMember).Distinct())
            {
                context.Report(
                    Code,
                    site,
                    $"'{gap.Detail}' names the type '{site.Key.Text}' is read in, and its getter returns no typeof or GetType() that could be read, so no rule could check the key there.");
            }
        }
    }
}
