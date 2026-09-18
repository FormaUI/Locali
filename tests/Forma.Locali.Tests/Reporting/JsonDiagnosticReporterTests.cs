namespace Forma.Locali.Tests.Reporting;

public sealed class JsonDiagnosticReporterTests
{
    [Fact]
    public async Task Should_write_a_report_naming_every_diagnostic()
    {
        // Arrange
        using var directory = new TempDirectory();
        var report = Path.Combine(directory.Root, "report.json");
        var diagnostic = new LintingDiagnostic("File.he.json", 2, 20, LintingSeverity.Error, LintingCodes.CultureMismatch, "Wrong.");

        // Act
        await new JsonDiagnosticReporter(report).ReportAsync(
            new LintingResult([diagnostic], 4, [], 0),
            TestContext.Current.CancellationToken);

        // Assert
        var written = LintReportTestReader.Read(await File.ReadAllTextAsync(report, TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.Equal(4, written.FileCount),
            () => Assert.Equal(1, written.DiagnosticCount),
            () => Assert.Equal(LintingCodes.CultureMismatch.Id, written.Diagnostics[0].Code),
            () => Assert.Equal("error", written.Diagnostics[0].Severity));
    }

    [Fact]
    public async Task Should_write_how_many_diagnostics_the_severity_left_out()
    {
        // Arrange
        using var directory = new TempDirectory();
        var report = Path.Combine(directory.Root, "report.json");

        // Act
        await new JsonDiagnosticReporter(report).ReportAsync(
            new LintingResult([], 4, [new SuppressedRule(LintingCodes.MissingLocalization, 2)], 0),
            TestContext.Current.CancellationToken);

        // Assert
        var written = LintReportTestReader.Read(await File.ReadAllTextAsync(report, TestContext.Current.CancellationToken));

        Assert.Equal(2, written.SuppressedCount);
    }

    [Fact]
    public async Task Should_write_how_many_diagnostics_a_files_own_configuration_left_out()
    {
        // Arrange
        using var directory = new TempDirectory();
        var report = Path.Combine(directory.Root, "report.json");

        // Act
        await new JsonDiagnosticReporter(report).ReportAsync(
            new LintingResult([], 4, [new SuppressedRule(LintingCodes.InconsistentLocalization, 3)], 3),
            TestContext.Current.CancellationToken);

        // Assert
        var written = LintReportTestReader.Read(await File.ReadAllTextAsync(report, TestContext.Current.CancellationToken));

        Assert.Equal(3, written.IgnoredCount);
    }

    [Fact]
    public async Task Should_create_the_directory_the_report_goes_into()
    {
        // Arrange
        using var directory = new TempDirectory();
        var report = Path.Combine(directory.Root, "nested", "deeper", "report.json");

        // Act
        await new JsonDiagnosticReporter(report).ReportAsync(
            new LintingResult([], 0, [], 0),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(File.Exists(report));
    }

    [Fact]
    public void Should_refuse_a_report_with_nowhere_to_go()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new JsonDiagnosticReporter("  "));
    }
}
