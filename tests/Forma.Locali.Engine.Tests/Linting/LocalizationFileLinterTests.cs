namespace Forma.Locali.Tests.Linting;

using Forma.Locali.Linting;

public sealed class LocalizationFileLinterTests
{
    [Fact]
    public async Task Should_hold_the_runtimes_config_file_to_its_rule()
    {
        // Arrange — a key the runtime does not read steers nothing, so it is reported where it is written.
        using var directory = new TempDirectory();
        var file = directory.Write(".locale.json", "{\n  \"paths\": [\"$RootDir/$AnchorName.$Culture$Extension\"],\n  \"rootNamespce\": \"Contoso.Widgets\"\n}");

        // Act
        var diagnostics = await new LocalizationFileLinter(SchemaLinterTestFactory.Create(), LocaleFormatTestFactory.Json).LintAsync(file, TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.MalformedLocaleConfig, diagnostic.Code),
            () => Assert.Equal(3, diagnostic.Line),
            () => Assert.Contains("'rootNamespce'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_report_a_config_file_that_is_not_readable_json()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(".locale.json", "not json");

        // Act
        var diagnostics = await new LocalizationFileLinter(SchemaLinterTestFactory.Create(), LocaleFormatTestFactory.Json).LintAsync(file, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([LintingCodes.MalformedLocaleConfig], diagnostics.Codes());
    }

    [Fact]
    public async Task Should_read_a_config_file_with_the_locale_file_rules_left_out()
    {
        // Arrange — the config file carries no metadata entry and no other entry, which is no fault of its own.
        using var directory = new TempDirectory();
        var file = directory.Write(".locale.json", """{ "rootNamespace": "Contoso.Widgets" }""");

        // Act
        var diagnostics = await new LocalizationFileLinter(SchemaLinterTestFactory.Create(), LocaleFormatTestFactory.Json).LintAsync(file, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Should_read_a_files_culture_without_the_folder_of_locale_files_its_project_names()
    {
        // Arrange — the file is named for fr, and its entry says he.
        using var directory = new TempDirectory();
        directory.Write("Contoso.Widgets.csproj", "<Project />");
        directory.Write(".locale.json", """{ "localesDir": ".strings" }""");
        var file = directory.Write(Path.Combine(".strings", "fr", "Widgets.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = await new LocalizationFileLinter(SchemaLinterTestFactory.Create(), LocaleFormatTestFactory.Json).LintAsync(file, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([LintingCodes.CultureMismatch], diagnostics.Codes());
    }

    [Fact]
    public async Task Should_report_a_file_without_a_marker_in_the_folder_of_locale_files_its_project_names()
    {
        // Arrange — the file's own name ends in no culture, so only its folder names it a locale file.
        using var directory = new TempDirectory();
        directory.Write("Contoso.Widgets.csproj", "<Project />");
        directory.Write(".locale.json", """{ "localesDir": ".strings" }""");
        var file = directory.Write(Path.Combine(".strings", "he", "Widgets.json"), LocalizationFileFakeData.Unmarked);

        // Act
        var diagnostics = await new LocalizationFileLinter(SchemaLinterTestFactory.Create(), LocaleFormatTestFactory.Json).LintAsync(file, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([LintingCodes.MissingMarker], diagnostics.Codes());
    }
}
