namespace Forma.Locali.Configuration;

using Forma.Locali.Diagnostics;

/// <summary>The hints a run's configuration names, its own and those of the files and packages it imports, with what is wrong in them.</summary>
/// <param name="LocaleMembers">Every member hint, in the order written.</param>
/// <param name="Diagnostics">What is wrong in the hints or in what imports them.</param>
public sealed record LocaleHints(IReadOnlyList<LocaleMemberHint> LocaleMembers, IReadOnlyList<LintingDiagnostic> Diagnostics)
{
    /// <summary>No hints at all.</summary>
    public static LocaleHints None { get; } = new([], []);
}
