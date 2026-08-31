namespace Forma.Locali.Rules.Sources;

using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

/// <summary>Checks that an expression naming an argument is given one.</summary>
public sealed class PluralArgumentChecker : ICallSiteRuleChecker
{
    /// <inheritdoc />
    public LintCode Code
        => LintCodes.UnsuppliedPluralArgument;

    /// <inheritdoc />
    public void Check(CallSiteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Supplied(context) is not { } supplied || !context.Expression.IsValid)
        {
            return;
        }

        foreach (var expression in context.Expression.Expressions)
        {
            if (expression.NameSpan.IsEmpty)
            {
                continue;
            }

            var name = expression.NameSpan.Slice(context.Site.Key).ToString();

            if (!supplied.Contains(name, StringComparer.Ordinal))
            {
                context.Report(
                    Code,
                    $"The expression reads '{name}', which the lookup does not supply, so it counts as zero.");
            }
        }
    }

    // Values paired by position or written into a hole are named after the key's holes.
    private static IReadOnlyList<string>? Supplied(CallSiteContext context)
        => context.Site.Arguments switch
        {
            LocalizerArguments.None => [],
            LocalizerArguments.Positional or LocalizerArguments.Interpolated => context.Holes,
            LocalizerArguments.Named => context.Site.Names,
            _ => null,
        };
}
