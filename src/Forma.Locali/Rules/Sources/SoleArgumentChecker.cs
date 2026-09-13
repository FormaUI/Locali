namespace Forma.Locali.Rules.Sources;

using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

/// <summary>Checks that an expression naming no argument is given the single value it reads.</summary>
public sealed class SoleArgumentChecker : ICallSiteRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.SoleArgumentExpected;

    /// <inheritdoc />
    public void Check(CallSiteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A lookup supplying nothing leaves the expression with no argument at all to read.
        if (context.Lookup.Arguments.Kind is not LocalizerArgumentKind.None || !context.Expression.IsValid)
        {
            return;
        }

        foreach (var expression in context.Expression.Expressions)
        {
            if (expression.NameSpan.IsEmpty)
            {
                context.Report(
                    Code,
                    "The expression names no argument, so it reads the only value supplied, but the lookup supplies none.");
            }
        }
    }
}
