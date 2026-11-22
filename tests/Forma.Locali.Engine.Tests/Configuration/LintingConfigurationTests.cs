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
    public void Should_leave_an_exclusion_that_matched_no_file_unreported()
    {
        // Arrange — a folder an exclusion names may exist on one machine alone, as a sandbox does, so the file's globs are held to no more than the command line's.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "excludeFiles": ["*/.sandbox/*"] }""");
        var file = directory.Write("Widget.he.json", "{}");
        var configuration = new LintingConfiguration(directory.Root);

        // Act
        var excluded = configuration.IsExcluded(file);

        // Assert
        Assert.Multiple(
            () => Assert.False(excluded),
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
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "Nowhere.he.json": ["LOC0113"] } }""");
        var file = directory.Write("Widget.he.json", "{}");
        var configuration = new LintingConfiguration(directory.Root);
        configuration.GetIgnoredCodes(file);

        // Act
        var diagnostics = configuration.GetDiagnostics();

        // Assert
        Assert.Equal([LintingCodes.StaleConfigurationEntry], diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_a_glob_that_matched_something_unreported()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "Widget.he.json": ["LOC0113"] } }""");
        var file = directory.Write("Widget.he.json", "{}");
        var configuration = new LintingConfiguration(directory.Root);
        configuration.GetIgnoredCodes(file);

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
    public void Should_warn_that_a_retired_rule_leaves_nothing_out_rather_than_fail_the_run()
    {
        // Arrange — a configuration written for a release that still reported LOC0117.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "Widget.he.json": ["LOC0117"] } }""");
        var file = directory.Write("Widget.he.json", "{}");
        var configuration = new LintingConfiguration(directory.Root);
        configuration.GetIgnoredCodes(file);

        // Act
        var diagnostics = configuration.GetDiagnostics();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Multiple(
            () => Assert.Equal((LintingCodes.StaleConfigurationEntry, LintingSeverity.Warning), (diagnostic.Code, diagnostic.Severity)),
            () => Assert.StartsWith("'LOC0117' is a rule this linter has retired", diagnostic.Message, StringComparison.Ordinal));
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
    [Fact]
    public void Should_read_the_configurations_own_hints_where_they_are_written()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(LintingConfiguration.FileName, """
            {
              "hints": {
                "localeMembers": [
                  { "assembly": "Contoso", "type": "Contoso.Component", "member": "L", "typeMember": "LocaleSourceType" }
                ]
              }
            }
            """);

        // Act
        var hints = new LintingConfiguration(directory.Root).ReadHints([]);

        // Assert
        var hint = Assert.Single(hints.LocaleMembers);
        Assert.Multiple(
            () => Assert.Equal("Contoso.Component.L", hint.MemberName),
            () => Assert.Equal((LintingConfiguration.FileName, 4), (Path.GetFileName(hint.Path), hint.Line)),
            () => Assert.Empty(hints.Diagnostics));
    }

    [Fact]
    public void Should_read_the_hints_a_project_of_the_run_carries_with_no_configuration_at_all()
    {
        // Arrange — a library's hints describe its own code, so a run building it reads them unasked.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Widgets", LocaleHintsFile.FileName), HintsFile);

        // Act
        var hints = new LintingConfiguration(directory.Root).ReadHints([Project(directory, "Widgets")]);

        // Assert
        var hint = Assert.Single(hints.LocaleMembers);
        Assert.Multiple(
            () => Assert.Equal(Path.GetFullPath(file), Path.GetFullPath(hint.Path)),
            () => Assert.Empty(hints.Diagnostics));
    }

    [Fact]
    public void Should_report_a_hints_file_that_does_not_follow_its_shape_in_that_file()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Widgets", LocaleHintsFile.FileName), """{ "localeMembers": [{ "assembly": "Contoso" }] }""");

        // Act
        var hints = new LintingConfiguration(directory.Root).ReadHints([Project(directory, "Widgets")]);

        // Assert
        var diagnostic = Assert.Single(hints.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.MalformedConfiguration, diagnostic.Code),
            () => Assert.Equal(Path.GetFullPath(file), Path.GetFullPath(diagnostic.Path)),
            () => Assert.Empty(hints.LocaleMembers));
    }

    [Fact]
    public void Should_read_the_hints_a_package_carries_where_the_run_restored_it_with_no_configuration_at_all()
    {
        // Arrange — a library's hints travel with its package, so a run restoring it reads them unasked.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("packages", "contoso.widgets", "1.0.0", LocaleHintsFile.FileName), HintsFile);

        // Act
        var hints = new LintingConfiguration(directory.Root).ReadHints([Package(directory)]);

        // Assert
        var hint = Assert.Single(hints.LocaleMembers);
        Assert.Multiple(
            () => Assert.Equal(Path.GetFullPath(file), Path.GetFullPath(hint.Path)),
            () => Assert.True(hint.IsPackaged),
            () => Assert.Empty(hints.Diagnostics));
    }

    [Fact]
    public void Should_read_the_key_hints_a_package_carries_and_leave_out_one_naming_an_assembly_it_does_not_ship()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(Path.Combine("packages", "contoso.widgets", "1.0.0", LocaleHintsFile.FileName), """
            {
              "localeKeys": [
                { "assembly": "Contoso.Widgets", "type": "Contoso.Widgets.Component", "method": "T", "parameter": "key", "locale": "L" },
                { "assembly": "Contoso.Other", "type": "Contoso.Other.Component", "method": "T", "parameter": "key", "locale": "L" }
              ]
            }
            """);

        // Act
        var hints = new LintingConfiguration(directory.Root).ReadHints([Package(directory)]);

        // Assert
        var hint = Assert.Single(hints.LocaleKeys);
        var diagnostic = Assert.Single(hints.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(("Contoso.Widgets.Component.T", true), (hint.MethodName, hint.IsPackaged)),
            () => Assert.Equal((LintingCodes.InvalidHint, LintingSeverity.Warning), (diagnostic.Code, diagnostic.Severity)));
    }

    [Fact]
    public void Should_report_a_librarys_hint_naming_an_assembly_the_library_does_not_build()
    {
        // Arrange — the hints a library ships speak for its own code alone.
        using var directory = new TempDirectory();
        directory.Write(Path.Combine("Widgets", LocaleHintsFile.FileName), """
            { "localeMembers": [{ "assembly": "Contoso.Other", "type": "Contoso.Other.Component", "member": "L", "typeMember": "LocaleSourceType" }] }
            """);

        // Act
        var hints = new LintingConfiguration(directory.Root).ReadHints([Project(directory, "Widgets")]);

        // Assert
        var diagnostic = Assert.Single(hints.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.InvalidHint, diagnostic.Code),
            () => Assert.Equal(LintingSeverity.Error, diagnostic.Severity),
            () => Assert.Empty(hints.LocaleMembers));
    }

    [Fact]
    public void Should_report_a_packages_hint_naming_an_assembly_it_does_not_ship_as_a_warning()
    {
        // Arrange — the package's file is the package's to fix, not the run's.
        using var directory = new TempDirectory();
        directory.Write(Path.Combine("packages", "contoso.widgets", "1.0.0", LocaleHintsFile.FileName), """
            { "localeMembers": [{ "assembly": "Contoso.Other", "type": "Contoso.Other.Component", "member": "L", "typeMember": "LocaleSourceType" }] }
            """);

        // Act
        var hints = new LintingConfiguration(directory.Root).ReadHints([Package(directory)]);

        // Assert
        var diagnostic = Assert.Single(hints.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.InvalidHint, diagnostic.Code),
            () => Assert.Equal(LintingSeverity.Warning, diagnostic.Severity),
            () => Assert.Empty(hints.LocaleMembers));
    }

    [Fact]
    public void Should_report_a_packages_hints_file_that_does_not_follow_its_shape_as_a_warning()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(Path.Combine("packages", "contoso.widgets", "1.0.0", LocaleHintsFile.FileName), """{ "localeMembers": [{ "assembly": "Contoso.Widgets" }] }""");

        // Act
        var hints = new LintingConfiguration(directory.Root).ReadHints([Package(directory)]);

        // Assert
        var diagnostic = Assert.Single(hints.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.MalformedConfiguration, diagnostic.Code),
            () => Assert.Equal(LintingSeverity.Warning, diagnostic.Severity));
    }

    private static HintsFolder Project(TempDirectory directory, string folder)
        => new(Path.Combine(directory.Root, folder), ["Contoso.Widgets"], IsPackage: false);

    private static HintsFolder Package(TempDirectory directory)
        => new(Path.Combine(directory.Root, "packages", "contoso.widgets", "1.0.0"), ["Contoso.Widgets"], IsPackage: true);

    private const string HintsFile = """
        {
          "localeMembers": [
            { "assembly": "Contoso.Widgets", "type": "Contoso.Widgets.Component", "member": "L", "typeMember": "LocaleSourceType" }
          ]
        }
        """;
}
