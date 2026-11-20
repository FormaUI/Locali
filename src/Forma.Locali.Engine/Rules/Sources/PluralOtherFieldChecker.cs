namespace Forma.Locali.Rules.Sources;

using Forma.Framework.Localization.Language;
using Forma.Locali.Diagnostics;

/// <summary>Checks that a plural expression carries the category the others fall back to.</summary>
public sealed class PluralOtherFieldChecker : ICallsiteRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.MissingPluralOther;

    /// <inheritdoc />
    public void Check(CallsiteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Expression.IsValid)
        {
            return;
        }

        var key = context.Lookup.Key.Text;
        foreach (var expression in context.Expression.Expressions)
        {
            if (LocaleExpressions.Names(expression, key, LocaleExpressionEvaluator.PluralKeyword) && !LocaleExpressions.Declares(expression, key, LocaleExpressions.OtherField))
            {
                context.Report(Code, $"The plural expression omits '{LocaleExpressions.OtherField}', so a count its categories miss renders as nothing.");
            }
        }
    }
}
