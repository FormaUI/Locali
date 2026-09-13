namespace Forma.Locali.Rules.Sources;

using Forma.Framework.Localization;
using Forma.Locali.Diagnostics;

/// <summary>Checks that a key is not nothing but holes, which carry no words to translate.</summary>
public sealed class BareHoleKeyChecker : ICallSiteRuleChecker
{
    /// <inheritdoc />
    public LintCode Code
        => LintCodes.BareHoleKey;

    /// <inheritdoc />
    public void Check(CallSiteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var hasHole = false;

        foreach (var segment in LocalizedSegment.Parse(context.Lookup.Key.Text))
        {
            if (segment.IsHole)
            {
                hasHole = true;
            }
            else if (Words.Any(segment.Text))
            {
                return;
            }
        }

        if (!hasHole)
        {
            return;
        }

        context.Report(
            Code,
            "The key carries holes but no words anywhere else, so there is no text here to translate; format the values directly instead.");
    }
}
