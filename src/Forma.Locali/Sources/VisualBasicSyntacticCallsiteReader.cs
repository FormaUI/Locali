namespace Forma.Locali.Sources;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

/// <summary>Reads the key out of an unbound Visual Basic lookup's arguments, mapped back to the file a human wrote.</summary>
internal static class VisualBasicSyntacticCallsiteReader
{
    private const string FormatMethod = "Format";
    private const string FormatParameter = "format";

    // Visual Basic opens a string with a straight, a curly or a fullwidth quote alike.
    private static readonly char[] Quotes = ['"', '“', '”', '＂'];

    /// <summary>The lookup its arguments describe, or null when the key is not a literal.</summary>
    /// <param name="tree">The tree the arguments were parsed from.</param>
    /// <param name="arguments">The arguments the lookup was given.</param>
    public static LocalizerLookup? Read(SyntaxTree tree, ArgumentListSyntax arguments)
    {
        var list = arguments.Arguments;

        // Without a bound overload, a value beyond the key cannot be told from a collection of them.
        return list.FirstOrDefault()?.GetExpression() is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? new LocalizerLookup(
                Literal(tree, literal),
                list.Count <= 1 ? LocalizerArguments.None : LocalizerArguments.Unknown,
                IsFormatString(arguments))
            : null;
    }

    /// <summary>Where a lookup writes its key as an interpolated string with holes, or null when it does not.</summary>
    /// <param name="tree">The tree the arguments were parsed from.</param>
    /// <param name="arguments">The arguments the lookup was given.</param>
    public static FileLinePositionSpan? InterpolatedKey(SyntaxTree tree, ArgumentListSyntax arguments)
        => arguments.Arguments.FirstOrDefault()?.GetExpression() is InterpolatedStringExpressionSyntax interpolated
            && interpolated.Contents.OfType<InterpolationSyntax>().Any()
            ? tree.GetMappedLineSpan(interpolated.Span)
            : null;

    /// <summary>Where a lookup writes the key that could not be read, or null when it writes no key at all.</summary>
    /// <param name="tree">The tree the arguments were parsed from.</param>
    /// <param name="arguments">The arguments the lookup was given.</param>
    public static FileLinePositionSpan? UnreadableKey(SyntaxTree tree, ArgumentListSyntax arguments)
        => arguments.Arguments.Count == 0 ? null : tree.GetMappedLineSpan(arguments.Arguments[0].Span);

    // The lookup's own expression is the argument list's parent, and the argument that carries it sits one above.
    private static bool IsFormatString(ArgumentListSyntax arguments)
        => arguments.Parent?.Parent is SimpleArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation } } argument
            && SuppliesFormatText(invocation, argument);

    // Without a bound overload, only the argument written first is certain, and only when it does not
    // itself claim to be a different parameter; a provider overload needs binding to tell apart.
    private static bool SuppliesFormatText(InvocationExpressionSyntax invocation, SimpleArgumentSyntax argument)
        => invocation.Expression is MemberAccessExpressionSyntax { Expression: { } receiver } member
            && VisualBasicNames.Equals(member.Name.Identifier.ValueText, FormatMethod)
            && IsStringType(receiver)
            && invocation.ArgumentList?.Arguments.FirstOrDefault() == argument
            && (argument.NameColonEquals is null || VisualBasicNames.Equals(argument.NameColonEquals.Name.Identifier.ValueText, FormatParameter));

    private static bool IsStringType(ExpressionSyntax expression)
        => expression switch
        {
            PredefinedTypeSyntax predefined => predefined.Keyword.IsKind(SyntaxKind.StringKeyword),
            IdentifierNameSyntax name => VisualBasicNames.Equals(name.Identifier.ValueText, "String"),
            MemberAccessExpressionSyntax member => VisualBasicNames.Equals(member.Name.Identifier.ValueText, "String"),
            _ => false,
        };

    private static LocalizationKeyReference Literal(SyntaxTree tree, LiteralExpressionSyntax literal)
        => new(literal.Token.ValueText, tree.GetMappedLineSpan(literal.Span), Offset(literal));

    // The key's text starts after the opening quote.
    private static int Offset(LiteralExpressionSyntax literal)
        => literal.Token.Text.Length - literal.Token.Text.TrimStart(Quotes).Length;
}
