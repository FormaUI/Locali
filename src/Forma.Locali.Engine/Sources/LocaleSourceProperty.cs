namespace Forma.Locali.Sources;

using Microsoft.CodeAnalysis;

/// <summary>A property naming the type a lookup through a member carrying <c>[LocaleSource]</c> reads for an instance.</summary>
/// <param name="Instance">The type of the instance.</param>
/// <param name="Property">The property, as the instance's type overrides it.</param>
public sealed record LocaleSourceProperty(INamedTypeSymbol Instance, IPropertySymbol Property);
