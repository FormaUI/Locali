namespace Forma.Locali.Rules;

using Forma.Framework.Localization;
using Forma.Framework.Localization.Language;
using Forma.Framework.Localization.Parser;
using Forma.Framework.Localization.Parser.Syntax;

/// <summary>The names a format refers to its values by.</summary>
internal static class HoleNames
{
    private static readonly LocaleKeywords Known = new(PluralExpressionEvaluator.Keyword);

    /// <summary>The distinct names the format's holes read, in the order they first appear.</summary>
    /// <param name="format">The format to read.</param>
    public static List<string> Distinct(string format)
    {
        var names = new List<string>();

        foreach (var segment in LocalizedSegment.Parse(format))
        {
            if (segment.IsHole)
            {
                Add(names, segment.Name.ToString());
            }
        }

        return names;
    }

    /// <summary>The expressions the format carries.</summary>
    /// <param name="format">The format to read.</param>
    public static IReadOnlyList<LocaleExpression> Expressions(string format)
        => LocaleExpressionParser.Parse(format, Known).Expressions;

    /// <summary>Every name the format fills, whether as a hole or as an expression's argument.</summary>
    /// <param name="format">The format to read.</param>
    public static List<string> Filled(string format)
    {
        var names = Distinct(format);

        foreach (var expression in Expressions(format))
        {
            // A plural expression names its argument in brackets rather than braces.
            if (!expression.NameSpan.IsEmpty)
            {
                Add(names, expression.NameSpan.Slice(format).ToString());
            }
        }

        return names;
    }

    private static void Add(List<string> names, string name)
    {
        if (!names.Contains(name, StringComparer.Ordinal))
        {
            names.Add(name);
        }
    }
}
