namespace Forma.Locali.Rules.Sources;

using Forma.Locali.Diagnostics;

/// <summary>Checks that a lookup is not itself passed to string.Format, which would format its already-translated result a second time.</summary>
public sealed class ReformattedLookupChecker : ICallSiteRuleChecker
{
    /// <inheritdoc />
    public LintCode Code
        => LintCodes.ReformattedLookup;

    /// <inheritdoc />
    public void Check(CallSiteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Site.IsFormatArgument)
        {
            context.Report(
                Code,
                "The lookup is passed to string.Format, which formats its own '{0}' rather than the runtime's holes, and throws once a translation names one; pass the values to the lookup itself instead.");
        }
    }
}
