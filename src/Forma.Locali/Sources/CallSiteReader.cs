namespace Forma.Locali.Sources;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Reads the key out of a lookup's arguments, mapped back to the file a human wrote.</summary>
internal static class CallSiteReader
{
    /// <summary>The name a lookup goes through when it is not an indexer.</summary>
    public const string ResolveMethod = "Resolve";

    /// <summary>The call site a lookup's arguments describe, or null when the key is not readable text.</summary>
    /// <param name="tree">The tree the arguments were parsed from.</param>
    /// <param name="arguments">The arguments the lookup was given.</param>
    /// <param name="symbol">The overload the lookup binds to, or null when nothing bound it.</param>
    /// <param name="model">The model that bound the arguments, or null when none did.</param>
    public static LocalizerCallSite? Read(SyntaxTree tree, BaseArgumentListSyntax arguments, ISymbol? symbol, SemanticModel? model)
    {
        var list = arguments.Arguments;

        return list.FirstOrDefault()?.Expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression)
                => Literal(tree, literal, Supplies(symbol, model, list), list.Count - 1),
            { } expression when Constant(expression, model) is { } text
                => Constant(tree, expression, text, Supplies(symbol, model, list), list.Count - 1),
            _ => null,
        };
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

    // The compiler folds a constant, so a key written as one is as readable as a literal spelled out.
    private static string? Constant(ExpressionSyntax expression, SemanticModel? model)
        => model?.GetConstantValue(expression) is { HasValue: true, Value: string text } ? text : null;

    // The span covers a reference rather than a quoted literal, so the key starts where the span does.
    private static LocalizerCallSite Constant(
        SyntaxTree tree,
        ExpressionSyntax expression,
        string key,
        LocalizerArguments supplies,
        int values)
        => new(
            key,
            tree.GetMappedLineSpan(expression.Span),
            0,
            supplies,
            values);

    // Passing the collection itself rather than its items leaves nothing to count.
    private static bool Collection(ITypeSymbol argument, ITypeSymbol parameter, ITypeSymbol element)
        => SymbolEqualityComparer.Default.Equals(argument, parameter)
            || (argument is IArrayTypeSymbol array && SymbolEqualityComparer.Default.Equals(array.ElementType, element));

    private static ITypeSymbol? Element(ITypeSymbol type)
        => type switch
        {
            IArrayTypeSymbol array => array.ElementType,
            INamedTypeSymbol { TypeArguments.Length: 1 } named => named.TypeArguments[0],
            _ => null,
        };

    private static LocalizerCallSite Literal(
        SyntaxTree tree,
        LiteralExpressionSyntax literal,
        LocalizerArguments supplies,
        int values)
        => new(
            literal.Token.ValueText,
            tree.GetMappedLineSpan(literal.Span),
            Offset(literal),
            supplies,
            values);

    // The key's text starts after the opening quote, which a raw or verbatim literal makes more than one character.
    private static int Offset(LiteralExpressionSyntax literal)
        => literal.Token.Text.Length - literal.Token.Text.TrimStart('@', '"').Length;

    private static LocalizerArguments Supplies(ISymbol? symbol, SemanticModel? model, SeparatedSyntaxList<ArgumentSyntax> list)
    {
        if (list.Count <= 1)
        {
            return LocalizerArguments.None;
        }

        var parameters = symbol switch
        {
            IPropertySymbol indexer => indexer.Parameters,
            IMethodSymbol method => method.Parameters,
            _ => default,
        };

        if (parameters.IsDefaultOrEmpty || Element(parameters[^1].Type) is not { } element)
        {
            return LocalizerArguments.Unknown;
        }

        if (list.Count == 2
            && model?.GetTypeInfo(list[1].Expression).Type is { } argument
            && Collection(argument, parameters[^1].Type, element))
        {
            return LocalizerArguments.Unknown;
        }

        return element.SpecialType is SpecialType.System_Object
            ? LocalizerArguments.Positional
            : LocalizerArguments.Unknown;
    }
}
