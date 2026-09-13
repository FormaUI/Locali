namespace Forma.Locali.Tests.Configuration;

using Forma.Locali.Configuration;

public sealed class LintingConfigurationTests
{
    [Fact]
    public void Should_ignore_a_file_a_nearby_configuration_names()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignore": { "Widget.he.json": ["LOC0113"] } }""");
        var file = directory.Write("Widget.he.json", "{}");

        // Act
        var ignored = new LintingConfiguration().GetIgnoredCodes(file);

        // Assert
        Assert.Equal([LintingCodes.InconsistentTranslation], ignored);
    }

    [Fact]
    public void Should_leave_a_file_with_no_configuration_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write("Widget.he.json", "{}");

        // Act
        var configuration = new LintingConfiguration();

        // Assert
        Assert.Multiple(
            () => Assert.Empty(configuration.GetIgnoredCodes(file)),
            () => Assert.False(configuration.IsExcluded(file)));
    }

    [Fact]
    public void Should_merge_an_ancestors_ignore_with_a_nearer_ones()
    {
        // Arrange — the root leaves out a call-site rule everywhere; the nearer file adds a file rule for its own name.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignore": { "**/*.he.json": ["LOC0209"] } }""");
        directory.Write(Path.Combine("Widgets", ".locali.json"), """{ "ignore": { "Widget.he.json": ["LOC0113"] } }""");
        var file = directory.Write(Path.Combine("Widgets", "Widget.he.json"), "{}");

        // Act
        var ignored = new LintingConfiguration().GetIgnoredCodes(file);

        // Assert
        Assert.Equal(
            new[] { LintingCodes.MissingTranslation, LintingCodes.InconsistentTranslation }.OrderBy(code => code.Id, StringComparer.Ordinal),
            ignored.OrderBy(code => code.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void Should_match_a_glob_relative_to_its_own_configurations_directory()
    {
        // Arrange — the glob is written as it would read from where the file sits beside the config.
        using var directory = new TempDirectory();
        directory.Write(Path.Combine("Widgets", ".locali.json"), """{ "ignore": { "Widget.he.json": ["LOC0113"] } }""");
        var file = directory.Write(Path.Combine("Widgets", "Widget.he.json"), "{}");

        // Act
        var ignored = new LintingConfiguration().GetIgnoredCodes(file);

        // Assert
        Assert.Equal([LintingCodes.InconsistentTranslation], ignored);
    }

    [Fact]
    public void Should_exclude_a_file_a_configuration_names()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "exclude": ["Fixtures/*"] }""");
        var file = directory.Write(Path.Combine("Fixtures", "Widget.he.json"), "{}");

        // Act & Assert
        Assert.True(new LintingConfiguration().IsExcluded(file));
    }

    [Fact]
    public void Should_never_let_a_configuration_ignore_the_rule_over_its_own_shape()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignore": { "Widget.he.json": ["LOC0020", "LOC0021"] } }""");
        var file = directory.Write("Widget.he.json", "{}");

        // Act & Assert
        Assert.Empty(new LintingConfiguration().GetIgnoredCodes(file));
    }

    [Fact]
    public void Should_report_a_glob_that_matched_no_file_the_run_reached()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignore": { "Nowhere.he.json": ["LOC0113"] }, "exclude": ["Nowhere/*"] }""");
        var file = directory.Write("Widget.he.json", "{}");
        var configuration = new LintingConfiguration();
        configuration.GetIgnoredCodes(file);
        configuration.IsExcluded(file);

        // Act
        var diagnostics = configuration.GetDiagnostics();

        // Assert
        Assert.Equal([LintingCodes.StaleConfigurationGlob, LintingCodes.StaleConfigurationGlob], diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_a_glob_that_matched_something_unreported()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignore": { "Widget.he.json": ["LOC0113"] }, "exclude": ["Widget.he.json"] }""");
        var file = directory.Write("Widget.he.json", "{}");
        var configuration = new LintingConfiguration();
        configuration.GetIgnoredCodes(file);
        configuration.IsExcluded(file);

        // Act
        var diagnostics = configuration.GetDiagnostics();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_rule_identifier_this_linter_does_not_define()
    {
        // Arrange — discovery is lazy, so a file has to be consulted before its configuration is read at all.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignore": { "Widget.he.json": ["LOC9999"] } }""");
        var file = directory.Write("Widget.he.json", "{}");
        var configuration = new LintingConfiguration();
        configuration.GetIgnoredCodes(file);

        // Act
        var diagnostics = configuration.GetDiagnostics();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.MalformedConfiguration, diagnostic.Code),
            () => Assert.Contains("LOC9999", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_report_a_configuration_file_that_is_not_readable_json()
    {
        // Arrange — discovery is lazy, so a file has to be consulted before its configuration is read at all.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", "not json");
        var file = directory.Write("Widget.he.json", "{}");
        var configuration = new LintingConfiguration();
        configuration.GetIgnoredCodes(file);

        // Act
        var diagnostics = configuration.GetDiagnostics();

        // Assert
        Assert.Equal([LintingCodes.MalformedConfiguration], diagnostics.Codes());
    }

    [Fact]
    public void Should_share_usage_of_the_same_file_discovered_from_two_directories()
    {
        // Arrange — two sibling files both walk past the same parent configuration.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignore": { "**/Widget.he.json": ["LOC0113"] } }""");
        var first = directory.Write(Path.Combine("A", "Widget.he.json"), "{}");
        var second = directory.Write(Path.Combine("B", "Widget.he.json"), "{}");
        var configuration = new LintingConfiguration();

        // Act
        configuration.GetIgnoredCodes(first);
        configuration.GetIgnoredCodes(second);

        // Assert — one discovery, matched from both places, so nothing is reported as stale.
        Assert.Empty(configuration.GetDiagnostics());
    }
}
