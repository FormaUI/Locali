namespace Forma.Locali.Tests.Linting;

public sealed class LintServiceTests
{
    [Fact]
    public async Task Should_report_nothing_for_a_file_that_follows_the_schema()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Valid);

        // Act
        var result = await LintServiceTestFactory.Create(file).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task Should_report_a_file_named_for_a_culture_that_carries_no_marker()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Unmarked);

        // Act
        var result = await LintServiceTestFactory.Create(file).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([LintCodes.MissingMarker], result.Diagnostics.Codes());
    }

    [Fact]
    public async Task Should_ignore_json_that_names_no_culture_and_carries_no_marker()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write("appsettings.json", """{ "Logging": { "Level": "Debug" } }""");

        // Act
        var result = await LintServiceTestFactory.Create(file).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task Should_search_a_directory_recursively()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write($"Nested/Deeper/{LocalizationFileFakeData.HebrewName}", LocalizationFileFakeData.Unmarked);

        // Act
        var result = await LintServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([LintCodes.MissingMarker], result.Diagnostics.Codes());
    }

    [Fact]
    public async Task Should_ignore_build_output()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write($"bin/Debug/{LocalizationFileFakeData.HebrewName}", LocalizationFileFakeData.Unmarked);
        directory.Write($"obj/{LocalizationFileFakeData.HebrewName}", LocalizationFileFakeData.Unmarked);

        // Act
        var result = await LintServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task Should_report_a_file_that_is_not_readable_json()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Unreadable);

        // Act
        var result = await LintServiceTestFactory.Create(file).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([LintCodes.InvalidJson], result.Diagnostics.Codes());
    }

    [Fact]
    public async Task Should_count_every_file_it_read()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Valid);
        directory.Write("Nested/Other.he.json", LocalizationFileFakeData.Valid);

        // Act
        var result = await LintServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.FileCount);
    }

    [Fact]
    public async Task Should_order_diagnostics_by_file()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write("Second.he.json", LocalizationFileFakeData.Unmarked);
        directory.Write("First.he.json", LocalizationFileFakeData.Unmarked);

        // Act
        var result = await LintServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([.. result.Diagnostics.Paths().Order(StringComparer.Ordinal)], result.Diagnostics.Paths());
    }

    [Fact]
    public async Task Should_leave_a_warning_out_when_the_run_reports_errors_only()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.TrailingMarker);

        // Act
        var result = await LintServiceTestFactory.Create(file).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(result.Diagnostics),
            () => Assert.Equal(1, result.SuppressedCount));
    }

    [Fact]
    public async Task Should_report_a_warning_when_the_run_reports_warnings()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.TrailingMarker);

        // Act
        var result = await LintServiceTestFactory.Create(file, LintSeverity.Warning)
            .RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([LintCodes.MarkerNotFirst], result.Diagnostics.Codes()),
            () => Assert.Equal(0, result.SuppressedCount));
    }
}
