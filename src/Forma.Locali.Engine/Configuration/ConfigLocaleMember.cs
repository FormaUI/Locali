namespace Forma.Locali.Configuration;

/// <summary>One item of a <c>localeMembers</c> hint list: a member returning a locale, and the member of the same instance returning the type it is read for.</summary>
/// <param name="Assembly">The name of the assembly declaring the member.</param>
/// <param name="Type">The full metadata name of the type declaring the member.</param>
/// <param name="Member">The property returning the locale.</param>
/// <param name="TypeMember">The property returning the type the locale is read for.</param>
/// <param name="Offset">The byte the item's opening brace sits at.</param>
public readonly record struct ConfigLocaleMember(string Assembly, string Type, string Member, string TypeMember, int Offset);
