namespace Forma.Locali.Rules;

using Forma.Framework.Localization.Parser;
using Forma.Framework.Localization.Parser.Syntax;

/// <summary>What the rules read of an expression a format carries: the keyword it names and the fields it declares.</summary>
internal static class LocaleExpressions
{
    /// <summary>The field every expression falls back to.</summary>
    public const string OtherField = "other";

    /// <summary>Whether an expression names a keyword.</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="format">The format the expression was parsed from.</param>
    /// <param name="keyword">The keyword.</param>
    public static bool Names(LocaleExpression expression, string format, LocaleKeyword keyword)
        => expression.KeywordSpan.Slice(format).SequenceEqual(keyword.Name);

    /// <summary>Whether an expression declares a field.</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="format">The format the expression was parsed from.</param>
    /// <param name="field">The field's name.</param>
    public static bool Declares(LocaleExpression expression, string format, string field)
    {
        foreach (var declared in expression.Fields)
        {
            if (declared.NameSpan.Slice(format).SequenceEqual(field))
            {
                return true;
            }
        }

        return false;
    }
}
