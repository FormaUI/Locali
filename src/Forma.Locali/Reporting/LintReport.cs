namespace Forma.Locali.Reporting;

/// <summary>The JSON report a run writes.</summary>
/// <param name="FileCount">How many files the run read.</param>
/// <param name="DiagnosticCount">How many diagnostics the run produced.</param>
/// <param name="SuppressedCount">How many diagnostics fell below the severity the run reports at.</param>
/// <param name="Diagnostics">The diagnostics themselves.</param>
public sealed record LintReport(int FileCount, int DiagnosticCount, int SuppressedCount, IReadOnlyList<LintReportEntry> Diagnostics);
