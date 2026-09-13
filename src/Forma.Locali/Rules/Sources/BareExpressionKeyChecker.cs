namespace Forma.Locali.Rules.Sources;

using Forma.Framework.Localization.Parser.Syntax;
using Forma.Locali.Diagnostics;

/// <summary>Checks that an expression key is not nothing but a count, which carries no words to translate.</summary>
public sealed class BareExpressionKeyChecker : ICallSiteRuleChecker
{
    /// <inheritdoc />
    public LintCode Code
        => LintCodes.BareExpressionKey;

    /// <inheritdoc />
    public void Check(CallSiteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var expressions = context.Expression.Expressions;

        if (!context.Expression.IsValid || expressions.Count == 0)
        {
            return;
        }

        var key = context.Lookup.Key.Text;

        if (WordsInAField(expressions, key) || WordsOutsideTheExpressions(expressions, key))
        {
            return;
        }

        context.Report(
            Code,
            "The expression carries no words in any field, only the count; format the value directly instead.");
    }

    private static bool WordsInAField(IReadOnlyList<LocaleExpression> expressions, string key)
    {
        foreach (var expression in expressions)
        {
            foreach (var field in expression.Fields)
            {
                if (Words.Any(field.ValueSpan.Slice(key)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool WordsOutsideTheExpressions(IReadOnlyList<LocaleExpression> expressions, string key)
    {
        var read = 0;

        // The parser walks the key from its start, so the spans arrive in order and never overlap.
        foreach (var expression in expressions)
        {
            if (Words.Any(key.AsSpan(read, expression.Span.Start - read)))
            {
                return true;
            }

            read = expression.Span.End;
        }

        return Words.Any(key.AsSpan(read));
    }
}
