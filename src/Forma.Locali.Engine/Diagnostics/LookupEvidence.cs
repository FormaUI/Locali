namespace Forma.Locali.Diagnostics;

/// <summary>How many checked lookups rest on each kind of evidence; a lookup held to types established in more than one way counts under each.</summary>
/// <param name="Analysis">How many were held to a type the flow reached on its own.</param>
/// <param name="VerifiedHint">How many were held to a type a hint relates them to and the flow reached as well.</param>
/// <param name="Hint">How many were held to a type a hint alone relates them to.</param>
public sealed record LookupEvidence(int Analysis, int VerifiedHint, int Hint);
