namespace Forma.Locali.Reporting;

/// <summary>The JSON report a run writes.</summary>
/// <param name="FileCount">How many files the run read.</param>
/// <param name="DiagnosticCount">How many diagnostics the run produced.</param>
/// <param name="SuppressedCount">How many diagnostics were left out, whether by severity or by a file's own configuration.</param>
/// <param name="IgnoredCount">How many of those a file's own configuration left out, rather than the severity.</param>
/// <param name="Diagnostics">The diagnostics themselves.</param>
public sealed record LintReport(int FileCount, int DiagnosticCount, int SuppressedCount, int IgnoredCount, IReadOnlyList<LintReportEntry> Diagnostics);
