namespace Forma.Locali.Diagnostics;

/// <summary>How far a run checked the lookups whose keys it read: whether each was held to every type it reads, what established those types, and why the rest went unchecked.</summary>
/// <param name="Lookups">How many lookups writing their key the run found in the files it linted.</param>
/// <param name="FullyChecked">How many were held to every type they read, each with entries the run could read.</param>
/// <param name="PartiallyChecked">How many were held to some of the types they read, while another path ended or its entries could not be read.</param>
/// <param name="Unchecked">How many were held to no type.</param>
/// <param name="Evidence">What established the types the checked lookups were held to.</param>
/// <param name="Reasons">How many lookups each reason left short of fully checked, by reason; a lookup short for several reasons counts under each.</param>
public sealed record LookupCoverage(int Lookups, int FullyChecked, int PartiallyChecked, int Unchecked, LookupEvidence Evidence, IReadOnlyDictionary<string, int> Reasons)
{
    /// <summary>A run that found no lookup.</summary>
    public static LookupCoverage None { get; } = new(0, 0, 0, 0, new LookupEvidence(0, 0, 0), new Dictionary<string, int>());
}
