namespace Forma.Locali.Rules.Sources;

using Forma.Framework.Localization;
using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

/// <summary>Checks that every hole of an interpolated key is a plain identifier.</summary>
public sealed class HoleIdentifierChecker : ICallSiteRuleChecker
{
    /// <inheritdoc />
    public LintCode Code
        => LintCodes.HoleNotIdentifier;

    /// <inheritdoc />
    public void Check(CallSiteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Site.Arguments is not LocalizerArguments.Interpolated)
        {
            return;
        }

        foreach (var hole in context.Site.Names)
        {
            if (!LocaleName.IsValid(hole))
            {
                context.Report(
                    Code,
                    $"The hole '{hole}' is not a plain identifier, so a translation cannot name it; assign it to a local first.");
            }
        }
    }
}
