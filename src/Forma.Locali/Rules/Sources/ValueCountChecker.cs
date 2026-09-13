namespace Forma.Locali.Rules.Sources;

using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

/// <summary>Checks that a lookup supplies one value for each hole its key names.</summary>
public sealed class ValueCountChecker : ICallSiteRuleChecker
{
    /// <inheritdoc />
    public LintCode Code
        => LintCodes.ValueCountMismatch;

    /// <inheritdoc />
    public void Check(CallSiteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Only values paired by position are counted against the key; named ones carry their own.
        if (context.Lookup.Arguments is not { Kind: LocalizerArgumentKind.Positional, ValueCount: { } supplied })
        {
            return;
        }

        var holes = context.Holes.Count;

        if (holes == 0 && supplied > 1)
        {
            context.Report(Code, $"The key names no hole, so it takes at most one value, but {supplied} are supplied.");
        }
        else if (holes != 0 && supplied != holes)
        {
            context.Report(Code, $"The key names {holes} hole(s) but {supplied} value(s) are supplied.");
        }
    }
}
