namespace Forma.Locali.Reporting;

using Forma.Locali.Diagnostics;

/// <summary>One diagnostic as the JSON report carries it.</summary>
/// <param name="Path">The file the diagnostic is about.</param>
/// <param name="Line">The one-based line.</param>
/// <param name="Column">The one-based column.</param>
/// <param name="Severity">How serious the diagnostic is.</param>
/// <param name="Code">The code identifying the check.</param>
/// <param name="Message">What the check found.</param>
public sealed record LintReportEntry(string Path, int Line, int Column, string Severity, string Code, string Message)
{
    /// <summary>Reads an entry off a diagnostic.</summary>
    /// <param name="diagnostic">The diagnostic to describe.</param>
    public static LintReportEntry From(LintDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        return new LintReportEntry(
            diagnostic.Path,
            diagnostic.Line,
            diagnostic.Column,
            diagnostic.SeverityLabel,
            diagnostic.Code.Id,
            diagnostic.Message);
    }
}
