namespace Forma.Locali.Tests.Linting;

using Forma.Locali.Projects;

public sealed class LintingServiceTests
{
    [Fact]
    public async Task Should_report_nothing_for_a_file_that_follows_the_schema()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Valid);

        // Act
        var result = await LintingServiceTestFactory.Create(file).RunAsync(TestContext.Current.CancellationToken);

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
        var result = await LintingServiceTestFactory.Create(file).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([LintingCodes.MissingMarker], result.Diagnostics.Codes());
    }

    [Fact]
    public async Task Should_ignore_json_that_names_no_culture_and_carries_no_marker()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write("appsettings.json", """{ "Logging": { "Level": "Debug" } }""");

        // Act
        var result = await LintingServiceTestFactory.Create(file).RunAsync(TestContext.Current.CancellationToken);

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
        var result = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([LintingCodes.MissingMarker], result.Diagnostics.Codes());
    }

    [Fact]
    public async Task Should_ignore_build_output()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write($"bin/Debug/{LocalizationFileFakeData.HebrewName}", LocalizationFileFakeData.Unmarked);
        directory.Write($"obj/{LocalizationFileFakeData.HebrewName}", LocalizationFileFakeData.Unmarked);

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

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
        var result = await LintingServiceTestFactory.Create(file).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([LintingCodes.InvalidJson], result.Diagnostics.Codes());
    }

    [Fact]
    public async Task Should_count_every_file_it_read()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Valid);
        directory.Write("Nested/Other.he.json", LocalizationFileFakeData.Valid);

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

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
        var result = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

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
        var result = await LintingServiceTestFactory.Create(file).RunAsync(TestContext.Current.CancellationToken);

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
        var result = await LintingServiceTestFactory.Create(file, LintingSeverity.Warning)
            .RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([LintingCodes.MarkerNotFirst], result.Diagnostics.Codes()),
            () => Assert.Equal(0, result.SuppressedCount));
    }

    [Fact]
    public async Task Should_leave_out_but_still_count_a_diagnostic_the_runs_own_configuration_ignores()
    {
        // Arrange — two sections translate one key two ways, which the linting configuration says is meant.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "Widget.he.json": ["LOC0113"] } }""");
        directory.Write("Widget.he.json", LocalizationFileFakeData.WithSections(("A", "Today", "היום"), ("B", "Today", "עכשיו")));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.DoesNotContain(LintingCodes.InconsistentLocalization, result.Diagnostics.Codes()),
            () => Assert.Equal(1, result.SuppressedCount),
            () => Assert.Equal(1, result.IgnoredCount));
    }

    [Fact]
    public async Task Should_report_the_runs_own_configuration_as_malformed()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", "not json");
        directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Valid);

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([LintingCodes.MalformedConfiguration], result.Diagnostics.Codes());
    }

    [Fact]
    public async Task Should_report_a_stale_glob_only_at_warning_severity()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "Nowhere.he.json": ["LOC0113"] } }""");
        directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Valid);

        // Act
        var errorsOnly = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);
        var warnings = await LintingServiceTestFactory.Create(directory.Root, LintingSeverity.Warning)
            .RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(errorsOnly.Diagnostics),
            () => Assert.Equal([LintingCodes.StaleConfigurationGlob], warnings.Diagnostics.Codes()));
    }

    [Fact]
    public async Task Should_exclude_a_file_the_runs_own_configuration_names()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "excludeFiles": ["*.he.json"] }""");
        directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Unmarked);

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(result.Diagnostics),
            () => Assert.Equal(0, result.FileCount));
    }

    [Fact]
    public async Task Should_report_an_embedded_config_marking_two_roots_and_finish_the_run()
    {
        // Arrange — two config files with none above them each mark a root, which the runtime refuses for the whole assembly.
        using var directory = new TempDirectory();
        var project = directory.Write("Scratch.csproj", """<Project Sdk="Microsoft.NET.Sdk" />""");
        var first = directory.Write(Path.Combine("Components", ".locale.json"), "{}");
        var second = directory.Write(Path.Combine("Pages", ".locale.json"), "{}");
        var file = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var resourceNames = new Dictionary<string, string>
        {
            [Path.GetFullPath(first)] = "Scratch.Components..locale.json",
            [Path.GetFullPath(second)] = "Scratch.Pages..locale.json",
            [Path.GetFullPath(file)] = "Scratch.Components..locales.he.json",
        };
        var compilation = CompilationTestFactory.Unreferenced("namespace Scratch.Components; public sealed class Widget;");
        var loader = new StubProjectLoader(new LoadedProject(project, compilation, false, false, EmbeddedResourceNames: resourceNames));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, projectLoader: loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.MalformedLocaleConfig, diagnostic.Code),
            () => Assert.Equal(project, diagnostic.Path),
            () => Assert.Contains(Path.GetFullPath(first), diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains(Path.GetFullPath(second), diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_report_a_locale_config_the_runtime_would_refuse()
    {
        // Arrange — the runtime's own config file sits among the locale files and is held to the runtime's rule.
        using var directory = new TempDirectory();
        var config = directory.Write(".locale.json", """{ "rootDir": "/strings" }""");

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.MalformedLocaleConfig, diagnostic.Code),
            () => Assert.Equal(config, diagnostic.Path),
            () => Assert.Equal(1, diagnostic.Line),
            () => Assert.Contains("not a relative path", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_leave_a_locale_config_the_runtime_reads_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locale.json", """{ "rootNamespace": "Contoso.Widgets", "paths": ["$RootDir/$AnchorName.$Culture$Extension"] }""");

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(result.Diagnostics),
            () => Assert.Equal(1, result.FileCount));
    }
}
