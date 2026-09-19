namespace Forma.Locali.Sources;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

/// <summary>Reads the key out of a lookup's arguments, mapped back to the file a human wrote.</summary>
internal static class CallsiteReader
{
    /// <summary>The name a lookup goes through when it is not an indexer.</summary>
    public const string ResolveMethod = "Resolve";

    /// <summary>The lookup its arguments describe, or null when the key is not readable text.</summary>
    /// <param name="tree">The tree the arguments were parsed from.</param>
    /// <param name="arguments">The arguments the lookup was given.</param>
    /// <param name="symbol">The overload the lookup binds to, or null when nothing bound it.</param>
    /// <param name="model">The model that bound the arguments, or null when none did.</param>
    public static LocalizerLookup? Read(SyntaxTree tree, BaseArgumentListSyntax arguments, ISymbol? symbol, SemanticModel? model)
    {
        var list = arguments.Arguments;
        var supplies = Supplies(symbol, model, list);
        var formatString = IsFormatString(arguments, model);

        return list.FirstOrDefault()?.Expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression)
                => new LocalizerLookup(Literal(tree, literal), supplies, formatString),
            { } expression when Constant(expression, model) is { } text
                => new LocalizerLookup(Constant(tree, expression, text), supplies, formatString),
            _ => null,
        };
    }

    // The lookup's own expression is the argument list's parent, whether it opens with a bracket or a paren.
    private static bool IsFormatString(BaseArgumentListSyntax arguments, SemanticModel? model)
        => arguments.Parent?.Parent is ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation } } argument
            && SuppliesFormatText(invocation, argument, model);

    // A value passed alongside the format text is no less an argument to string.Format, so the parameter the
    // lookup actually bound to is what decides this, not where it sits in the source; a named argument can
    // reorder them freely.
    private static bool SuppliesFormatText(InvocationExpressionSyntax invocation, ArgumentSyntax argument, SemanticModel? model)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Format" } member)
        {
            return false;
        }

        if (model is not null)
        {
            return model.GetSymbolInfo(invocation).Symbol is IMethodSymbol { ContainingType.SpecialType: SpecialType.System_String }
                && model.GetOperation(argument) is IArgumentOperation { Parameter.Name: "format" };
        }

        // Without a bound overload, only the argument written first is certain, and only when it does not
        // itself claim to be a different parameter; a provider overload needs binding to tell apart.
        return IsStringType(member.Expression)
            && invocation.ArgumentList.Arguments.FirstOrDefault() == argument
            && argument.NameColon is null or { Name.Identifier.ValueText: "format" };
    }

    private static bool IsStringType(ExpressionSyntax expression)
        => expression switch
        {
            PredefinedTypeSyntax { Keyword.ValueText: "string" } => true,
            IdentifierNameSyntax { Identifier.ValueText: "String" } => true,
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "String" } => true,
            _ => false,
        };

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
    private static LocalizationKeyReference Constant(SyntaxTree tree, ExpressionSyntax expression, string key)
        => new(key, tree.GetMappedLineSpan(expression.Span), 0);

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

    private static LocalizationKeyReference Literal(SyntaxTree tree, LiteralExpressionSyntax literal)
        => new(literal.Token.ValueText, tree.GetMappedLineSpan(literal.Span), Offset(literal));

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
            ? LocalizerArguments.Positional(list.Count - 1)
            : LocalizerArguments.Unknown;
    }
}
