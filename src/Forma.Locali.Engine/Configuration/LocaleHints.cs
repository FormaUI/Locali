namespace Forma.Locali.Configuration;

using Forma.Locali.Diagnostics;

/// <summary>The hints a run reads, its configuration's own and those each library of the run carries, with what is wrong in them.</summary>
/// <param name="LocaleMembers">Every member hint, in the order written.</param>
/// <param name="LocaleKeys">Every key hint, in the order written.</param>
/// <param name="Diagnostics">What is wrong in the hints or in the files holding them.</param>
public sealed record LocaleHints(IReadOnlyList<LocaleMemberHint> LocaleMembers, IReadOnlyList<LocaleKeyHint> LocaleKeys, IReadOnlyList<LintingDiagnostic> Diagnostics)
{
    /// <summary>No hints at all.</summary>
    public static LocaleHints None { get; } = new([], [], []);
}
