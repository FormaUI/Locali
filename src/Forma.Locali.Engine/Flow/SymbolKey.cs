namespace Forma.Locali.Flow;

using Microsoft.CodeAnalysis;

/// <summary>A symbol's identity across the compilations of a run: its assembly and declaration id, or the symbol itself where it has no id, as a lambda's parameter has none.</summary>
internal readonly record struct SymbolKey
{
    private SymbolKey(string? id, ISymbol? symbol)
    {
        Id = id;
        Symbol = symbol;
    }

    /// <summary>The symbol's assembly and declaration id, or null where it has no id.</summary>
    public string? Id { get; }

    /// <summary>The symbol itself, where it has no id.</summary>
    public ISymbol? Symbol { get; }

    /// <summary>The key of a symbol, by its original definition.</summary>
    /// <param name="symbol">The symbol.</param>
    public static SymbolKey Of(ISymbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        var definition = symbol.OriginalDefinition;

        return definition switch
        {
            IParameterSymbol parameter when DeclarationId(parameter.ContainingSymbol) is { } owner => new($"{owner}#{parameter.Ordinal}", null),
            ITypeParameterSymbol typeParameter when DeclarationId(typeParameter.ContainingSymbol) is { } owner => new($"{owner}`{typeParameter.Ordinal}", null),
            IParameterSymbol or ITypeParameterSymbol => new(null, definition),
            _ when DeclarationId(definition) is { } id => new(id, null),
            _ => new(null, definition),
        };
    }

    /// <inheritdoc />
    public bool Equals(SymbolKey other)
        => Id is not null || other.Id is not null
            ? string.Equals(Id, other.Id, StringComparison.Ordinal)
            : SymbolEqualityComparer.Default.Equals(Symbol, other.Symbol);

    /// <inheritdoc />
    public override int GetHashCode()
        => Id?.GetHashCode(StringComparison.Ordinal) ?? SymbolEqualityComparer.Default.GetHashCode(Symbol);

    private static string? DeclarationId(ISymbol symbol)
        => symbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction }
            ? null
            : DocumentationCommentId.CreateDeclarationId(symbol.OriginalDefinition) is { } id
                ? $"{symbol.ContainingAssembly?.Name}|{id}"
                : null;
}
