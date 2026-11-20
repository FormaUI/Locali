namespace Forma.Locali.Rules.Sources;

using Forma.Framework.Localization.Language;
using Forma.Locali.Diagnostics;

/// <summary>Checks that a gender expression carries the field the genders it does not name fall back to.</summary>
public sealed class GenderOtherFieldChecker : ICallsiteRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.MissingGenderOther;

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
            if (LocaleExpressions.Names(expression, key, LocaleExpressionEvaluator.GenderKeyword) && !LocaleExpressions.Declares(expression, key, LocaleExpressions.OtherField))
            {
                context.Report(Code, $"The gender expression omits '{LocaleExpressions.OtherField}', so a gender its fields do not name, or none at all, renders as nothing.");
            }
        }
    }
}
