namespace Forma.Locali.Flow;

using Forma.Locali.Configuration;

using Microsoft.CodeAnalysis;

/// <summary>A type a lookup reads, with where its path began.</summary>
/// <param name="Type">The type, by its open definition, as the runtime's factory reads it.</param>
/// <param name="Root">Where the path to the type began, as a reader would recognize it in the code.</param>
/// <param name="IsApproximate">Whether the path joins alternatives whose pairing with the lookup the analysis could not follow, so the type may not be the one the lookup reads.</param>
/// <param name="Evidence">What established the type.</param>
/// <param name="Hint">The hint that supplied or verified the type, where one did.</param>
public sealed record LocaleOrigin(INamedTypeSymbol Type, string Root, bool IsApproximate = false, LocaleEvidence Evidence = LocaleEvidence.Analysis, LocaleMemberHint? Hint = null);
