namespace Forma.Locali.Diagnostics;

/// <summary>What a run looked at and what it found.</summary>
/// <param name="Diagnostics">Every diagnostic the run reports, ordered by file and then by position.</param>
/// <param name="FileCount">How many files the run read.</param>
/// <param name="Suppressed">What each rule contributed below the severity the run reports at, or that a file's own configuration left out, ordered by code.</param>
/// <param name="IgnoredCount">How many of the suppressed diagnostics the linting configuration left out, rather than the severity.</param>
public sealed record LintingResult(
    IReadOnlyList<LintingDiagnostic> Diagnostics,
    int FileCount,
    IReadOnlyList<SuppressedRule> Suppressed,
    int IgnoredCount)
{
    /// <summary>How many diagnostics in total were left out, whether by severity or by a file's own configuration.</summary>
    public int SuppressedCount
        => Suppressed.Sum(rule => rule.Count);
}
