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
        await reporter.ReportAsync(new LintingResult([], 3, [], 0), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Linted 3 file(s); no diagnostics.", writer.ToString().Trim());
    }

    [Fact]
    public async Task Should_write_every_diagnostic_before_the_summary()
    {
        // Arrange
        await using var writer = new StringWriter();
        var reporter = new ConsoleDiagnosticReporter(writer);
        var diagnostic = new LintingDiagnostic("File.he.json", 2, 5, LintingSeverity.Error, LintingCodes.MissingMarker, "no marker");

        // Act
        await reporter.ReportAsync(new LintingResult([diagnostic], 1, [], 0), TestContext.Current.CancellationToken);

        // Assert
        var lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal([diagnostic.ToString(), "Linted 1 file(s); 1 diagnostic(s)."], lines);
    }

    [Fact]
    public async Task Should_brief_the_diagnostics_the_severity_left_out()
    {
        // Arrange — a file rule and two call-site rules, so the brief has both families to line up.
        await using var writer = new StringWriter();
        var reporter = new ConsoleDiagnosticReporter(writer);

        SuppressedRule[] suppressed =
        [
            new(LintingCodes.MarkerNotFirst, 2),
            new(LintingCodes.MissingLocalization, 4),
            new(LintingCodes.KeyNotAnalysed, 1),
        ];

        // Act
        await reporter.ReportAsync(new LintingResult([], 3, suppressed, 0), TestContext.Current.CancellationToken);

        // Assert — the blank line is asserted too, so the brief keeps its distance from the summary.
        var lines = writer.ToString().TrimEnd().Split(Environment.NewLine);

        Assert.Equal(
            [
                "Linted 3 file(s); no diagnostics.",
                string.Empty,
                "7 diagnostic(s) not shown:",
                "  - LOC0102 (2)",
                "  - LOC0209 (4), LOC0210 (1)",
                "Pass --severity warning to read them.",
            ],
            lines);
    }

    [Fact]
    public async Task Should_brief_diagnostics_a_files_own_configuration_left_out()
    {
        // Arrange — every one of them was ignored, none by severity, so raising it would reveal nothing.
        await using var writer = new StringWriter();
        var reporter = new ConsoleDiagnosticReporter(writer);
        SuppressedRule[] suppressed = [new(LintingCodes.InconsistentLocalization, 2)];

        // Act
        await reporter.ReportAsync(new LintingResult([], 1, suppressed, 2), TestContext.Current.CancellationToken);

        // Assert
        var lines = writer.ToString().TrimEnd().Split(Environment.NewLine);

        Assert.Equal(
            [
                "Linted 1 file(s); no diagnostics.",
                string.Empty,
                "2 diagnostic(s) not shown:",
                "  - LOC0113 (2)",
                "The linting configuration accounts for all of them.",
            ],
            lines);
    }

    [Fact]
    public async Task Should_brief_a_mix_of_severity_and_configuration()
    {
        // Arrange — a file rule for the severity-suppressed one, a call-site rule for the ignored one.
        await using var writer = new StringWriter();
        var reporter = new ConsoleDiagnosticReporter(writer);
        SuppressedRule[] suppressed = [new(LintingCodes.MarkerNotFirst, 1), new(LintingCodes.MissingLocalization, 1)];

        // Act
        await reporter.ReportAsync(new LintingResult([], 1, suppressed, 1), TestContext.Current.CancellationToken);

        // Assert
        var lines = writer.ToString().TrimEnd().Split(Environment.NewLine);

        Assert.Equal(
            [
                "Linted 1 file(s); no diagnostics.",
                string.Empty,
                "2 diagnostic(s) not shown:",
                "  - LOC0102 (1)",
                "  - LOC0209 (1)",
                "Pass --severity warning to read the rest; the linting configuration accounts for the others.",
            ],
            lines);
    }
}
