namespace Forma.Locali.Tests.Configuration;

using Forma.Locali.Configuration;

public sealed class LintingConfigurationTests
{
    [Fact]
    public void Should_ignore_a_file_a_nearby_configuration_names()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "Widget.he.json": ["LOC0113"] } }""");
        var file = directory.Write("Widget.he.json", "{}");

        // Act
        var ignored = new LintingConfiguration().GetIgnoredCodes(file);

        // Assert
        Assert.Equal([LintingCodes.InconsistentLocalization], ignored);
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
    public void Should_merge_an_ancestors_ignore_diagnostics_with_a_nearer_ones()
    {
        // Arrange — the root leaves out a callsite rule everywhere; the nearer file adds a file rule for its own name.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "**/*.he.json": ["LOC0207"] } }""");
        directory.Write(Path.Combine("Widgets", ".locali.json"), """{ "ignoreDiagnostics": { "Widget.he.json": ["LOC0113"] } }""");
        var file = directory.Write(Path.Combine("Widgets", "Widget.he.json"), "{}");

        // Act
        var ignored = new LintingConfiguration().GetIgnoredCodes(file);

        // Assert
        Assert.Equal(
            new[] { LintingCodes.MissingLocalization, LintingCodes.InconsistentLocalization }.OrderBy(code => code.Id, StringComparer.Ordinal),
            ignored.OrderBy(code => code.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void Should_let_the_nearest_locale_sources_win()
    {
        // Arrange — an allow list cannot be merged by union, so the nearer file restates the policy for its own tree.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "localeSources": ["pack", "host"] }""");
        directory.Write(Path.Combine("Showcase", ".locali.json"), """{ "localeSources": ["embedded", "pack", "host"] }""");
        var nested = directory.Write(Path.Combine("Showcase", "Pages", "he.json"), "{}");
        var outer = directory.Write(Path.Combine("Widgets", "he.json"), "{}");

        // Act
        var configuration = new LintingConfiguration();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LocaleSources.Embedded | LocaleSources.Pack | LocaleSources.Host, configuration.GetLocaleSources(nested)),
            () => Assert.Equal(LocaleSources.Pack | LocaleSources.Host, configuration.GetLocaleSources(outer)));
    }

    [Fact]
    public void Should_report_a_locale_sources_entry_no_file_resolved_to()
    {
        // Arrange — the nearer file restates the list for the only folder holding a locale file, so the root's entry governs nothing.
        using var directory = new TempDirectory();
        var root = directory.Write(".locali.json", """{ "localeSources": ["pack"] }""");
        directory.Write(Path.Combine("Showcase", ".locali.json"), """{ "localeSources": ["embedded"] }""");
        var file = directory.Write(Path.Combine("Showcase", "he.json"), "{}");
        var configuration = new LintingConfiguration();
        _ = configuration.GetLocaleSources(file);

        // Act
        var diagnostics = configuration.GetDiagnostics();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.StaleConfigurationEntry, diagnostic.Code),
            () => Assert.Equal(root, diagnostic.Path),
            () => Assert.Contains("'localeSources'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_leave_a_locale_sources_entry_a_file_resolved_to_unreported()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "localeSources": ["pack"] }""");
        var file = directory.Write("he.json", "{}");
        var configuration = new LintingConfiguration();
        _ = configuration.GetLocaleSources(file);

        // Act
        var diagnostics = configuration.GetDiagnostics();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_allow_every_locale_source_when_no_configuration_names_them()
    {
        // Arrange — a nearer file naming other entries does not unsay a farther one's sources, and none at all leaves the policy unset.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "localeSources": ["pack"] }""");
        directory.Write(Path.Combine("Widgets", ".locali.json"), """{ "excludeFiles": [] }""");
        var governed = directory.Write(Path.Combine("Widgets", "he.json"), "{}");
        using var bare = new TempDirectory();
        var ungoverned = bare.Write("he.json", "{}");

        // Act
        var configuration = new LintingConfiguration();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LocaleSources.Pack, configuration.GetLocaleSources(governed)),
            () => Assert.Null(configuration.GetLocaleSources(ungoverned)));
    }

    [Fact]
    public void Should_match_a_glob_relative_to_its_own_configurations_directory()
    {
        // Arrange — the glob is written as it would read from where the file sits beside the config.
        using var directory = new TempDirectory();
        directory.Write(Path.Combine("Widgets", ".locali.json"), """{ "ignoreDiagnostics": { "Widget.he.json": ["LOC0113"] } }""");
        var file = directory.Write(Path.Combine("Widgets", "Widget.he.json"), "{}");

        // Act
        var ignored = new LintingConfiguration().GetIgnoredCodes(file);

        // Assert
        Assert.Equal([LintingCodes.InconsistentLocalization], ignored);
    }

    [Fact]
    public void Should_exclude_a_file_a_configuration_names()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "excludeFiles": ["Fixtures/*"] }""");
        var file = directory.Write(Path.Combine("Fixtures", "Widget.he.json"), "{}");

        // Act & Assert
        Assert.True(new LintingConfiguration().IsExcluded(file));
    }

    [Fact]
    public void Should_never_let_a_configuration_ignore_the_rule_over_its_own_shape()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "Widget.he.json": ["LOC0020", "LOC0021"] } }""");
        var file = directory.Write("Widget.he.json", "{}");

        // Act & Assert
        Assert.Empty(new LintingConfiguration().GetIgnoredCodes(file));
    }

    [Fact]
    public void Should_report_a_glob_that_matched_no_file_the_run_reached()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "Nowhere.he.json": ["LOC0113"] }, "excludeFiles": ["Nowhere/*"] }""");
        var file = directory.Write("Widget.he.json", "{}");
        var configuration = new LintingConfiguration();
        configuration.GetIgnoredCodes(file);
        configuration.IsExcluded(file);

        // Act
        var diagnostics = configuration.GetDiagnostics();

        // Assert
        Assert.Equal([LintingCodes.StaleConfigurationEntry, LintingCodes.StaleConfigurationEntry], diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_a_glob_that_matched_something_unreported()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "Widget.he.json": ["LOC0113"] }, "excludeFiles": ["Widget.he.json"] }""");
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
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "Widget.he.json": ["LOC9999"] } }""");
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
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "**/Widget.he.json": ["LOC0113"] } }""");
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
