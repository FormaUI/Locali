namespace Forma.Locali.Sources;

using Microsoft.CodeAnalysis;

/// <summary>A lookup taking its key from a parameter of its own method, whose calls write the key.</summary>
/// <param name="Lookup">The lookup's node.</param>
/// <param name="Key">Where the lookup writes the parameter as its key.</param>
/// <param name="Parameter">The parameter's name.</param>
public sealed record ForwardedKey(SyntaxNode Lookup, FileLinePositionSpan Key, string Parameter)
{
    /// <summary>Whether the lookup's method is a library's public one, whose consumers write the key at calls outside the run.</summary>
    public bool IsCalledFromOutside { get; init; }
}
