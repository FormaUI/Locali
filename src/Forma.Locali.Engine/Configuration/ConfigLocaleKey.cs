namespace Forma.Locali.Configuration;

/// <summary>One item of a <c>localeKeys</c> hint list: a method whose parameter its body looks up as a key, and where the locale it reads comes from.</summary>
/// <param name="Assembly">The name of the assembly declaring the method.</param>
/// <param name="Type">The full metadata name of the type declaring the method.</param>
/// <param name="Method">The method's name.</param>
/// <param name="Parameter">The parameter the method looks up as a key.</param>
/// <param name="Locale">The parameter of the method, or the member of its type, holding the locale the key is looked up in, or null where a type parameter names the type.</param>
/// <param name="TypeParameter">The type parameter of the method naming the type the key is looked up in, or null where a locale holds it.</param>
/// <param name="Offset">The byte the item's opening brace sits at.</param>
public readonly record struct ConfigLocaleKey(string Assembly, string Type, string Method, string Parameter, string? Locale, string? TypeParameter, int Offset);
