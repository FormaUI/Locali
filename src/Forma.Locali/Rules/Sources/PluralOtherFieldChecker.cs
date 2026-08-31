namespace Forma.Locali.Rules.Sources;

using Forma.Framework.Localization.Language;
using Forma.Framework.Localization.Parser.Syntax;
using Forma.Locali.Diagnostics;

/// <summary>Checks that a plural expression carries the category the others fall back to.</summary>
public sealed class PluralOtherFieldChecker : ICallSiteRuleChecker
{
    private const string Fallback = "other";

    /// <inheritdoc />
    public LintCode Code
        => LintCodes.MissingPluralOther;

    /// <inheritdoc />
    public void Check(CallSiteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Expression.IsValid)
        {
            return;
        }

        foreach (var expression in context.Expression.Expressions)
        {
            if (!expression.KeywordSpan.Slice(context.Site.Key).SequenceEqual(PluralExpressionEvaluator.Keyword.Name))
            {
                continue;
            }

            if (!Declares(expression.Fields, context.Site.Key))
            {
                context.Report(Code, $"The plural expression omits '{Fallback}', so a count its categories miss renders as nothing.");
            }
        }
    }

    private static bool Declares(IReadOnlyList<LocaleField> fields, string key)
    {
        foreach (var field in fields)
        {
            if (field.NameSpan.Slice(key).SequenceEqual(Fallback))
            {
                return true;
            }
        }

        return false;
    }
}
