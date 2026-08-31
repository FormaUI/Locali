namespace Forma.Locali.Sources;

using System.Text;

using Forma.Framework.Localization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Reads the key out of a lookup's arguments, mapped back to the file a human wrote.</summary>
internal static class CallSiteReader
{
    /// <summary>The name a lookup goes through when it is not an indexer.</summary>
    public const string ResolveMethod = "Resolve";

    private const string UnnamedMethod = "Unnamed";

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
            InterpolatedStringExpressionSyntax interpolated => Interpolated(tree, interpolated),
            { } expression when Constant(expression, model) is { } text
                => Constant(tree, expression, text, Supplies(symbol, model, list), list.Count - 1),
            _ => null,
        };
    }

    // The compiler folds a constant, so a key written as one is as readable as a literal spelled out.
    private static string? Constant(ExpressionSyntax expression, SemanticModel? model)
        => model?.GetConstantValue(expression) is { HasValue: true, Value: string text } ? text : null;

    // The span covers a reference rather than a quoted literal, so the key starts where the span does.
    private static LocalizerCallSite Constant(
        SyntaxTree tree,
        ExpressionSyntax expression,
        string key,
        (LocalizerArguments Kind, IReadOnlyList<string> Names) supplies,
        int values)
        => new(
            key,
            tree.GetMappedLineSpan(expression.Span),
            0,
            supplies.Kind,
            values,
            supplies.Names);

    /// <summary>Where a lookup writes the key that could not be read, or null when it writes no key at all.</summary>
    /// <param name="tree">The tree the arguments were parsed from.</param>
    /// <param name="arguments">The arguments the lookup was given.</param>
    public static FileLinePositionSpan? UnreadableKey(SyntaxTree tree, BaseArgumentListSyntax arguments)
        => arguments.Arguments.Count == 0 ? null : tree.GetMappedLineSpan(arguments.Arguments[0].Span);

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

    private static string Hole(StringBuilder key, InterpolationSyntax interpolation)
    {
        // The compiler passes the hole's text as written, which is the name a translation has to spell.
        var name = interpolation.Expression.ToString();

        key.Append('{').Append(name);

        if (interpolation.AlignmentClause is { Value: { } alignment } && alignment.ToString() != "0")
        {
            key.Append(',').Append(alignment.ToString());
        }

        if (interpolation.FormatClause is { } format)
        {
            key.Append(':').Append(format.FormatStringToken.ValueText);
        }

        key.Append('}');

        return name;
    }

    // The handler rebuilds the key from the parts, so reading it here has to reproduce that text.
    private static LocalizerCallSite Interpolated(SyntaxTree tree, InterpolatedStringExpressionSyntax interpolated)
    {
        var key = new StringBuilder();
        var holes = new List<string>();

        foreach (var content in interpolated.Contents)
        {
            switch (content)
            {
                case InterpolatedStringTextSyntax text:
                    Literal(key, text.TextToken.ValueText);

                    break;

                case InterpolationSyntax interpolation:
                    holes.Add(Hole(key, interpolation));

                    break;
            }
        }

        return new LocalizerCallSite(
            key.ToString(),
            tree.GetMappedLineSpan(interpolated.Span),
            0,
            LocalizerArguments.Interpolated,
            holes.Count,
            holes);
    }

    // A brace in prose is doubled so it survives the walk that later reads the key's holes.
    private static void Literal(StringBuilder key, string value)
    {
        foreach (var character in value)
        {
            if (character is '{' or '}')
            {
                key.Append(character);
            }

            key.Append(character);
        }
    }

    private static LocalizerCallSite Literal(
        SyntaxTree tree,
        LiteralExpressionSyntax literal,
        (LocalizerArguments Kind, IReadOnlyList<string> Names) supplies,
        int values)
        => new(
            literal.Token.ValueText,
            tree.GetMappedLineSpan(literal.Span),
            Offset(literal),
            supplies.Kind,
            values,
            supplies.Names);

    // A value names itself through a tuple, a construction, or the helper for the nameless one.
    private static string? Named(ExpressionSyntax expression)
        => expression switch
        {
            TupleExpressionSyntax { Arguments.Count: 2 } tuple => Text(tuple.Arguments[0].Expression),
            ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 2 } creation
                => Text(creation.ArgumentList.Arguments[0].Expression),
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member }
                when member.Name.Identifier.ValueText == UnnamedMethod => string.Empty,
            _ => null,
        };

    // One value whose name cannot be read leaves the whole list unjudgeable.
    private static (LocalizerArguments Kind, IReadOnlyList<string> Names) Names(SeparatedSyntaxList<ArgumentSyntax> list)
    {
        var names = new List<string>(list.Count - 1);

        for (var index = 1; index < list.Count; index++)
        {
            if (Named(list[index].Expression) is not { } name)
            {
                return (LocalizerArguments.Unknown, []);
            }

            names.Add(name);
        }

        return (LocalizerArguments.Named, names);
    }

    // The key's text starts after the opening quote, which a raw or verbatim literal makes more than one character.
    private static int Offset(LiteralExpressionSyntax literal)
        => literal.Token.Text.Length - literal.Token.Text.TrimStart('@', '$', '"').Length;

    private static (LocalizerArguments Kind, IReadOnlyList<string> Names) Supplies(
        ISymbol? symbol,
        SemanticModel? model,
        SeparatedSyntaxList<ArgumentSyntax> list)
    {
        if (list.Count <= 1)
        {
            return (LocalizerArguments.None, []);
        }

        var parameters = symbol switch
        {
            IPropertySymbol indexer => indexer.Parameters,
            IMethodSymbol method => method.Parameters,
            _ => default,
        };

        if (parameters.IsDefaultOrEmpty || Element(parameters[^1].Type) is not { } element)
        {
            return (LocalizerArguments.Unknown, []);
        }

        if (list.Count == 2
            && model?.GetTypeInfo(list[1].Expression).Type is { } argument
            && Collection(argument, parameters[^1].Type, element))
        {
            return (LocalizerArguments.Unknown, []);
        }

        return element switch
        {
            { Name: nameof(LocalizedArgument) } => Names(list),
            { SpecialType: SpecialType.System_Object } => (LocalizerArguments.Positional, []),
            _ => (LocalizerArguments.Unknown, []),
        };
    }

    private static string? Text(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : null;
}
