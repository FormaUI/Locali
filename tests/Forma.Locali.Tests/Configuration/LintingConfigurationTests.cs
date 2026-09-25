namespace Forma.Locali.Tests.Configuration;

using Forma.Locali.Configuration;

public sealed class LintingConfigurationTests
{
    [Fact]
    public void Should_ignore_a_file_the_configuration_names()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "Widget.he.json": ["LOC0113"] } }""");
        var file = directory.Write("Widget.he.json", "{}");

        // Act
        var ignored = new LintingConfiguration(directory.Root).GetIgnoredCodes(file);

        // Assert
        Assert.Equal([LintingCodes.InconsistentLocalization], ignored);
    }

    [Fact]
    public void Should_leave_a_run_with_no_configuration_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write("Widget.he.json", "{}");

        // Act
        var configuration = new LintingConfiguration(directory.Root);

        // Assert
        Assert.Multiple(
            () => Assert.Null(configuration.FilePath),
            () => Assert.Empty(configuration.GetIgnoredCodes(file)),
            () => Assert.False(configuration.IsExcluded(file)),
            () => Assert.Null(configuration.GetLocaleSources(file)),
            () => Assert.Empty(configuration.GetDiagnostics()));
    }

    [Fact]
    public void Should_read_the_configuration_nearest_above_the_path_and_no_other()
    {
        // Arrange — the run is given the root, so the file beneath it is not read, whatever it names.
        using var directory = new TempDirectory();
        var root = directory.Write(".locali.json", """{ "ignoreDiagnostics": { "**/*.he.json": ["LOC0207"] } }""");
        directory.Write(Path.Combine("Widgets", ".locali.json"), """{ "ignoreDiagnostics": { "Widget.he.json": ["LOC0113"] } }""");
        var file = directory.Write(Path.Combine("Widgets", "Widget.he.json"), "{}");

        // Act
        var configuration = new LintingConfiguration(directory.Root);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(root, configuration.FilePath),
            () => Assert.Equal([LintingCodes.MissingLocalization], configuration.GetIgnoredCodes(file)));
    }

    [Fact]
    public void Should_find_the_configuration_above_a_path_given_as_a_file()
    {
        // Arrange
        using var directory = new TempDirectory();
        var root = directory.Write(".locali.json", """{ "excludeFiles": [] }""");
        var file = directory.Write(Path.Combine("Widgets", "Widget.he.json"), "{}");

        // Act
        var configuration = new LintingConfiguration(file);

        // Assert
        Assert.Equal(root, configuration.FilePath);
    }

    [Fact]
    public void Should_find_the_configuration_nearest_above_a_deeper_path()
    {
        // Arrange — a run given the nested folder reads the nested file and never the root's.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "**/*.he.json": ["LOC0207"] } }""");
        var nested = directory.Write(Path.Combine("Widgets", ".locali.json"), """{ "ignoreDiagnostics": { "Widget.he.json": ["LOC0113"] } }""");
        var file = directory.Write(Path.Combine("Widgets", "Widget.he.json"), "{}");

        // Act
        var configuration = new LintingConfiguration(Path.Combine(directory.Root, "Widgets"));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(nested, configuration.FilePath),
            () => Assert.Equal([LintingCodes.InconsistentLocalization], configuration.GetIgnoredCodes(file)));
    }

    [Fact]
    public void Should_match_a_glob_relative_to_the_configurations_directory()
    {
        // Arrange — the glob is written as it reads from where the configuration sits, whatever the run was given.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "Widgets/Widget.he.json": ["LOC0113"] } }""");
        var file = directory.Write(Path.Combine("Widgets", "Widget.he.json"), "{}");

        // Act
        var ignored = new LintingConfiguration(Path.Combine(directory.Root, "Widgets")).GetIgnoredCodes(file);

        // Assert
        Assert.Equal([LintingCodes.InconsistentLocalization], ignored);
    }

    [Fact]
    public void Should_let_the_last_locale_sources_line_matching_a_file_win()
    {
        // Arrange — lines are written general first and specific after, and an allow list cannot merge, so the last match speaks.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """
            {
              "localeSources": {
                "**": ["pack", "host"],
                "Showcase/**": ["embedded", "pack", "host"],
                "Showcase/Strict/**": ["embedded"]
              }
            }
            """);
        var widget = directory.Write(Path.Combine("Widgets", "he.json"), "{}");
        var page = directory.Write(Path.Combine("Showcase", "Pages", "he.json"), "{}");
        var strict = directory.Write(Path.Combine("Showcase", "Strict", "he.json"), "{}");

        // Act
        var configuration = new LintingConfiguration(directory.Root);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LocaleSources.Pack | LocaleSources.Host, configuration.GetLocaleSources(widget)),
            () => Assert.Equal(LocaleSources.Embedded | LocaleSources.Pack | LocaleSources.Host, configuration.GetLocaleSources(page)),
            () => Assert.Equal(LocaleSources.Embedded, configuration.GetLocaleSources(strict)));
    }

    [Fact]
    public void Should_leave_a_file_no_locale_sources_line_matches_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "localeSources": { "Packs/**": ["pack"] } }""");
        var file = directory.Write(Path.Combine("Widgets", "he.json"), "{}");

        // Act
        var allowed = new LintingConfiguration(directory.Root).GetLocaleSources(file);

        // Assert
        Assert.Null(allowed);
    }

    [Fact]
    public void Should_report_a_locale_sources_line_that_is_the_last_match_for_no_file()
    {
        // Arrange — the general line comes last, so it claims every file the specific one matches, and the specific one governs nothing.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "localeSources": { "Showcase/**": ["pack"], "**": ["embedded"] } }""");
        var file = directory.Write(Path.Combine("Showcase", "he.json"), "{}");
        var configuration = new LintingConfiguration(directory.Root);
        _ = configuration.GetLocaleSources(file);

        // Act
        var diagnostics = configuration.GetDiagnostics();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.StaleConfigurationEntry, diagnostic.Code),
            () => Assert.Contains("'Showcase/**'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_leave_a_locale_sources_line_a_file_resolved_to_unreported()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "localeSources": { "**": ["pack"] } }""");
        var file = directory.Write("he.json", "{}");
        var configuration = new LintingConfiguration(directory.Root);
        _ = configuration.GetLocaleSources(file);

        // Act
        var diagnostics = configuration.GetDiagnostics();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_exclude_a_file_a_configuration_names()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "excludeFiles": ["Fixtures/*"] }""");
        var file = directory.Write(Path.Combine("Fixtures", "Widget.he.json"), "{}");

        // Act & Assert
        Assert.True(new LintingConfiguration(directory.Root).IsExcluded(file));
    }

    [Fact]
    public void Should_exclude_a_file_the_command_line_names()
    {
        // Arrange — the command line's globs are part of the run's configuration, matched against the full path.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Fixtures", "Widget.he.json"), "{}");

        // Act & Assert
        Assert.True(new LintingConfiguration(directory.Root, ["*/Fixtures/*"]).IsExcluded(file));
    }

    [Fact]
    public void Should_count_a_file_exclude_the_command_line_also_names_as_applied()
    {
        // Arrange — the gate command passes the same glob the file holds; the file's entry is asked first and always, so it is not stale.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "excludeFiles": ["*/Fixtures/*"] }""");
        var file = directory.Write(Path.Combine("Suite", "Fixtures", "Broken.he.json"), "{}");
        var configuration = new LintingConfiguration(directory.Root, ["*/Fixtures/*"]);

        // Act
        var excluded = configuration.IsExcluded(file);

        // Assert
        Assert.Multiple(
            () => Assert.True(excluded),
            () => Assert.Empty(configuration.GetDiagnostics()));
    }

    [Fact]
    public void Should_never_let_a_configuration_ignore_the_rule_over_its_own_shape()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "Widget.he.json": ["LOC0020", "LOC0021"] } }""");
        var file = directory.Write("Widget.he.json", "{}");

        // Act & Assert
        Assert.Empty(new LintingConfiguration(directory.Root).GetIgnoredCodes(file));
    }

    [Fact]
    public void Should_report_a_glob_that_matched_no_file_the_run_reached()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "Nowhere.he.json": ["LOC0113"] }, "excludeFiles": ["Nowhere/*"] }""");
        var file = directory.Write("Widget.he.json", "{}");
        var configuration = new LintingConfiguration(directory.Root);
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
        var configuration = new LintingConfiguration(directory.Root);
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
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "Widget.he.json": ["LOC9999"] } }""");
        var file = directory.Write("Widget.he.json", "{}");
        var configuration = new LintingConfiguration(directory.Root);
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
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", "not json");

        // Act
        var diagnostics = new LintingConfiguration(directory.Root).GetDiagnostics();

        // Assert
        Assert.Equal([LintingCodes.MalformedConfiguration], diagnostics.Codes());
    }

    [Fact]
    public void Should_count_a_globs_usage_once_across_every_file_it_matches()
    {
        // Arrange — two files in different folders match one glob, which is then used, not stale.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "**/Widget.he.json": ["LOC0113"] } }""");
        var first = directory.Write(Path.Combine("A", "Widget.he.json"), "{}");
        var second = directory.Write(Path.Combine("B", "Widget.he.json"), "{}");
        var configuration = new LintingConfiguration(directory.Root);

        // Act
        configuration.GetIgnoredCodes(first);
        configuration.GetIgnoredCodes(second);

        // Assert
        Assert.Empty(configuration.GetDiagnostics());
    }
}
