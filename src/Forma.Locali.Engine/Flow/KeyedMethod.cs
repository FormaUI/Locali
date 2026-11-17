namespace Forma.Locali.Flow;

using Forma.Locali.Configuration;

using Microsoft.CodeAnalysis;

/// <summary>A method a key hint is bound to: the parameter it looks up as a key, and where the locale it reads comes from, exactly one of a parameter, a member or a type parameter.</summary>
/// <param name="Hint">The hint.</param>
/// <param name="Method">The method's declaration.</param>
/// <param name="KeyOrdinal">The ordinal of the parameter the method looks up as a key.</param>
/// <param name="LocaleParameter">The ordinal of the parameter holding the locale, or null.</param>
/// <param name="LocaleMember">The property or field of the method's type holding the locale, or null.</param>
/// <param name="TypeParameter">The ordinal of the type parameter naming the type the key is looked up in, or null.</param>
internal sealed record KeyedMethod(LocaleKeyHint Hint, IMethodSymbol Method, int KeyOrdinal, int? LocaleParameter, ISymbol? LocaleMember, int? TypeParameter);
