namespace Forma.Locali.Diagnostics;

/// <summary>What a run looked at and what it found.</summary>
/// <param name="Diagnostics">Every diagnostic the run reports, ordered by file and then by position.</param>
/// <param name="FileCount">How many files the run read.</param>
/// <param name="Suppressed">What each rule contributed below the severity the run reports at, ordered by code.</param>
public sealed record LintResult(
    IReadOnlyList<LintDiagnostic> Diagnostics,
    int FileCount,
    IReadOnlyList<SuppressedRule> Suppressed)
{
    /// <summary>How many diagnostics in total fell below the severity the run reports at.</summary>
    public int SuppressedCount
        => Suppressed.Sum(rule => rule.Count);
}
