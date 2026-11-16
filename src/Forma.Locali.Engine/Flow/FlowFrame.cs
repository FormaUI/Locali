namespace Forma.Locali.Flow;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

/// <summary>A member the flow is evaluating: the compilation holding its source, the instance it runs on, and the arguments and type arguments the call that entered it passed.</summary>
internal sealed class FlowFrame
{
    /// <summary>The compilation holding the source the frame's operations were bound in.</summary>
    public required Compilation Compilation { get; init; }

    /// <summary>The instance the member runs on, or null for a static member.</summary>
    public FlowValue? This { get; init; }

    /// <summary>The argument each parameter was given by the call that entered the member, with the frame the call ran in; none when the member was reached without a call.</summary>
    public IReadOnlyDictionary<SymbolKey, (IOperation Value, FlowFrame Frame)> Arguments { get; init; } = new Dictionary<SymbolKey, (IOperation, FlowFrame)>();

    /// <summary>The type each type parameter was given by the call that entered the member.</summary>
    public IReadOnlyDictionary<SymbolKey, ITypeSymbol> TypeArguments { get; init; } = new Dictionary<SymbolKey, ITypeSymbol>();

    /// <summary>How many calls deep the frame sits below the lookup.</summary>
    public int Depth { get; init; }
}
