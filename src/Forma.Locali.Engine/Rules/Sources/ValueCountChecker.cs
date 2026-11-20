namespace Forma.Locali.Rules.Sources;

using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

/// <summary>Checks that a lookup supplies one value for each name its key's values pair with, its holes and its expressions' arguments.</summary>
public sealed class ValueCountChecker : ICallsiteRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.ValueCountMismatch;

    /// <inheritdoc />
    public void Check(CallsiteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Only values paired by position are counted against the key; named ones carry their own.
        if (context.Lookup.Arguments is not { Kind: LocalizerArgumentKind.Positional, ValueCount: { } supplied })
        {
            return;
        }

        var names = context.Names.Count;

        if (names == 0 && supplied > 1)
        {
            context.Report(Code, $"The key names no hole or argument, so it takes at most one value, but {supplied} are supplied.");
        }
        else if (names != 0 && supplied != names)
        {
            context.Report(Code, $"The key names {names} hole(s) and argument(s) but {supplied} value(s) are supplied.");
        }
    }
}
