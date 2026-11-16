namespace Forma.Locali.Flow;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

/// <summary>A call writing as text the key a lookup takes from its method's parameter, with what the lookup's locale is in the frames the call enters.</summary>
/// <param name="Call">The call.</param>
/// <param name="KeyOrdinal">The ordinal of the parameter the call's key binds to.</param>
/// <param name="Compilation">The compilation holding the call.</param>
/// <param name="Values">What the lookup's locale is when the call reaches it.</param>
internal sealed record ForwardedCall(IInvocationOperation Call, int KeyOrdinal, Compilation Compilation, IReadOnlyList<FlowValue> Values);
