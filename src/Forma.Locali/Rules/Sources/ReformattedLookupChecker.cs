namespace Forma.Locali.Rules.Sources;

using Forma.Locali.Diagnostics;

/// <summary>Checks that a lookup does not itself supply string.Format's format text, which would format its already-translated result a second time.</summary>
public sealed class ReformattedLookupChecker : ICallSiteRuleChecker
{
    /// <inheritdoc />
    public LintCode Code
        => LintCodes.ReformattedLookup;

    /// <inheritdoc />
    public void Check(CallSiteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Lookup.IsFormatString)
        {
            context.Report(
                Code,
                "The lookup supplies string.Format's format text, which reads its own '{0}' rather than the runtime's holes, and throws once a translation names one; pass the values to the lookup itself instead.");
        }
    }
}
