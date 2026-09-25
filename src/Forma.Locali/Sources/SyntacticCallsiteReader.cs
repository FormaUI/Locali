namespace Forma.Locali.Sources;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Reads the key out of an unbound lookup's arguments, mapped back to the file a human wrote.</summary>
internal static class SyntacticCallsiteReader
{
    /// <summary>The lookup its arguments describe, or null when the key is not a literal.</summary>
    /// <param name="tree">The tree the arguments were parsed from.</param>
    /// <param name="arguments">The arguments the lookup was given.</param>
    public static LocalizerLookup? Read(SyntaxTree tree, BaseArgumentListSyntax arguments)
    {
        var list = arguments.Arguments;

        // Without a bound overload, a value beyond the key cannot be told from a collection of them.
        return list.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? new LocalizerLookup(
                Literal(tree, literal),
                list.Count <= 1 ? LocalizerArguments.None : LocalizerArguments.Unknown,
                IsFormatString(arguments))
            : null;
    }

    /// <summary>Where a lookup writes its key as an interpolated string with holes, or null when it does not.</summary>
    /// <param name="tree">The tree the arguments were parsed from.</param>
    /// <param name="arguments">The arguments the lookup was given.</param>
    public static FileLinePositionSpan? InterpolatedKey(SyntaxTree tree, BaseArgumentListSyntax arguments)
        => arguments.Arguments.FirstOrDefault()?.Expression is InterpolatedStringExpressionSyntax interpolated
            && interpolated.Contents.OfType<InterpolationSyntax>().Any()
            ? tree.GetMappedLineSpan(interpolated.Span)
            : null;

    /// <summary>Where a lookup writes the key that could not be read, or null when it writes no key at all.</summary>
    /// <param name="tree">The tree the arguments were parsed from.</param>
    /// <param name="arguments">The arguments the lookup was given.</param>
    public static FileLinePositionSpan? UnreadableKey(SyntaxTree tree, BaseArgumentListSyntax arguments)
        => arguments.Arguments.Count == 0 ? null : tree.GetMappedLineSpan(arguments.Arguments[0].Span);

    // The lookup's own expression is the argument list's parent, whether it opens with a bracket or a paren.
    private static bool IsFormatString(BaseArgumentListSyntax arguments)
        => arguments.Parent?.Parent is ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation } } argument
            && SuppliesFormatText(invocation, argument);

    // Without a bound overload, only the argument written first is certain, and only when it does not
    // itself claim to be a different parameter; a provider overload needs binding to tell apart.
    private static bool SuppliesFormatText(InvocationExpressionSyntax invocation, ArgumentSyntax argument)
        => invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Format" } member
            && IsStringType(member.Expression)
            && invocation.ArgumentList.Arguments.FirstOrDefault() == argument
            && argument.NameColon is null or { Name.Identifier.ValueText: "format" };

    private static bool IsStringType(ExpressionSyntax expression)
        => expression switch
        {
            PredefinedTypeSyntax { Keyword.ValueText: "string" } => true,
            IdentifierNameSyntax { Identifier.ValueText: "String" } => true,
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "String" } => true,
            _ => false,
        };

    private static LocalizationKeyReference Literal(SyntaxTree tree, LiteralExpressionSyntax literal)
        => new(literal.Token.ValueText, tree.GetMappedLineSpan(literal.Span), Offset(literal));

    // The key's text starts after the opening quote, which a raw or verbatim literal makes more than one character.
    private static int Offset(LiteralExpressionSyntax literal)
        => literal.Token.Text.Length - literal.Token.Text.TrimStart('@', '"').Length;
}
