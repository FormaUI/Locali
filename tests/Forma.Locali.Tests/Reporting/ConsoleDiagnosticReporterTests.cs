namespace Forma.Locali.Tests.Reporting;

public sealed class ConsoleDiagnosticReporterTests
{
    [Fact]
    public async Task Should_write_a_clean_summary_when_nothing_was_found()
    {
        // Arrange
        await using var writer = new StringWriter();
        var reporter = new ConsoleDiagnosticReporter(writer);

        // Act
        await reporter.ReportAsync(new LintResult([], 3, []), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Linted 3 file(s); no diagnostics.", writer.ToString().Trim());
    }

    [Fact]
    public async Task Should_write_every_diagnostic_before_the_summary()
    {
        // Arrange
        await using var writer = new StringWriter();
        var reporter = new ConsoleDiagnosticReporter(writer);
        var diagnostic = new LintDiagnostic("File.he.json", 2, 5, LintSeverity.Error, LintCodes.MissingMarker, "no marker");

        // Act
        await reporter.ReportAsync(new LintResult([diagnostic], 1, []), TestContext.Current.CancellationToken);

        // Assert
        var lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal([diagnostic.ToString(), "Linted 1 file(s); 1 diagnostic(s)."], lines);
    }

    [Fact]
    public async Task Should_brief_the_warnings_the_severity_left_out()
    {
        // Arrange — a file rule and two call-site rules, so the brief has both families to line up.
        await using var writer = new StringWriter();
        var reporter = new ConsoleDiagnosticReporter(writer);

        SuppressedRule[] suppressed =
        [
            new(LintCodes.MarkerNotFirst, 2),
            new(LintCodes.MissingTranslation, 4),
            new(LintCodes.KeyNotAnalysed, 1),
        ];

        // Act
        await reporter.ReportAsync(new LintResult([], 3, suppressed), TestContext.Current.CancellationToken);

        // Assert — the blank line is asserted too, so the brief keeps its distance from the summary.
        var lines = writer.ToString().TrimEnd().Split(Environment.NewLine);

        Assert.Equal(
            [
                "Linted 3 file(s); no diagnostics.",
                string.Empty,
                "7 warning(s) not shown:",
                "  - LOC0102 (2)",
                "  - LOC0209 (4), LOC0210 (1)",
                "Pass --severity warning to read them.",
            ],
            lines);
    }
}
