namespace Forma.Locali.Flow;

using Microsoft.CodeAnalysis;

/// <summary>A write to a field or a property: where it is, the value written, and the member writing it.</summary>
/// <param name="Kind">Where the write is.</param>
/// <param name="Value">The value written, or null where the write is no plain value.</param>
/// <param name="Owner">The member writing it, or null for an initializer or code outside the type.</param>
internal readonly record struct FlowWrite(FlowWriteKind Kind, IOperation? Value, IMethodSymbol? Owner);
