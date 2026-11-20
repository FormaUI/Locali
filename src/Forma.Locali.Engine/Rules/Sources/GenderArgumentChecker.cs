namespace Forma.Locali.Rules.Sources;

using Forma.Framework.Localization.Language;
using Forma.Locali.Diagnostics;

/// <summary>Checks that a gender expression naming an argument is given one.</summary>
public sealed class GenderArgumentChecker : ICallsiteRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.UnsuppliedGenderArgument;

    /// <inheritdoc />
    public void Check(CallsiteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.SuppliedNames is not { } supplied || !context.Expression.IsValid)
        {
            return;
        }

        var key = context.Lookup.Key.Text;
        foreach (var expression in context.Expression.Expressions)
        {
            if (expression.NameSpan.IsEmpty || !LocaleExpressions.Names(expression, key, LocaleExpressionEvaluator.GenderKeyword))
            {
                continue;
            }

            var name = expression.NameSpan.Slice(key).ToString();
            if (!supplied.Contains(name, StringComparer.Ordinal))
            {
                context.Report(
                    Code,
                    $"The expression reads '{name}', which the lookup does not supply, so it always selects '{LocaleExpressions.OtherField}'.");
            }
        }
    }
}
