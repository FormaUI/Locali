namespace Forma.Locali.Tests.Configuration;

using System.Text;

using Forma.Locali.Configuration;

public sealed class LocaliConfigFileTests
{
    [Fact]
    public void Should_read_a_globs_codes_with_their_offsets()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "ignoreDiagnostics": { "**/LocalizationPage.razor": ["LOC0207", "LOC0113"] } }""");

        // Assert
        var entry = Assert.Single(document.IgnoreDiagnostics);

        Assert.Multiple(
            () => Assert.Equal("**/LocalizationPage.razor", entry.Glob),
            () => Assert.Equal(["LOC0207", "LOC0113"], entry.Entries.Select(diagnostic => diagnostic.Id)));
    }

    [Fact]
    public void Should_read_the_exclude_files_globs_with_their_offsets()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "excludeFiles": ["*/Fixtures/*"] }""");

        // Assert
        var entry = Assert.Single(document.ExcludeFiles);

        Assert.Equal("*/Fixtures/*", entry.Glob);
    }

    [Fact]
    public void Should_read_each_locale_sources_line_in_order_with_its_sources_combined()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "localeSources": { "**": ["pack", "host"], "src/Forma.AddOns/*": ["embedded"] } }""");

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["**", "src/Forma.AddOns/*"], document.LocaleSources.Select(entry => entry.Glob)),
            () => Assert.Equal([LocaleSources.Pack | LocaleSources.Host, LocaleSources.Embedded], document.LocaleSources.Select(entry => entry.Allowed)),
            () => Assert.Empty(document.MalformedValues));
    }

    [Fact]
    public void Should_read_the_entries_written_after_locale_sources()
    {
        // Arrange — the object is read through to its end, so the entry after it is not lost.
        var document = LocaliConfigFileTestFactory.Read("""{ "localeSources": { "**": ["pack"] }, "excludeFiles": ["*/Fixtures/*"] }""");

        // Assert
        Assert.Multiple(
            () => Assert.Single(document.LocaleSources),
            () => Assert.Single(document.ExcludeFiles),
            () => Assert.Empty(document.MalformedValues));
    }

    [Fact]
    public void Should_read_no_locale_sources_lines_when_the_file_names_none()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "excludeFiles": [] }""");

        // Assert
        Assert.Empty(document.LocaleSources);
    }

    [Fact]
    public void Should_flag_a_locale_source_the_linter_does_not_define()
    {
        // Arrange — the known sources still count, so the list is read to its end.
        var document = LocaliConfigFileTestFactory.Read("""{ "localeSources": { "**": ["embedded", "package"] } }""");

        // Assert
        var malformedValue = Assert.Single(document.MalformedValues);
        Assert.Multiple(
            () => Assert.Contains("'package'", malformedValue.Reason, StringComparison.Ordinal),
            () => Assert.Equal(LocaleSources.Embedded, Assert.Single(document.LocaleSources).Allowed));
    }

    [Fact]
    public void Should_flag_locale_sources_that_are_not_an_object()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "localeSources": ["pack"] }""");

        // Assert
        var malformedValue = Assert.Single(document.MalformedValues);
        Assert.Multiple(
            () => Assert.Contains("'localeSources'", malformedValue.Reason, StringComparison.Ordinal),
            () => Assert.Empty(document.LocaleSources));
    }

    [Fact]
    public void Should_flag_a_locale_sources_line_whose_sources_are_not_a_list()
    {
        // Arrange — the line after the bad one is still read.
        var document = LocaliConfigFileTestFactory.Read("""{ "localeSources": { "src/**": "pack", "**": ["host"] } }""");

        // Assert
        var malformedValue = Assert.Single(document.MalformedValues);
        Assert.Multiple(
            () => Assert.Contains("'src/**'", malformedValue.Reason, StringComparison.Ordinal),
            () => Assert.Equal(["**"], document.LocaleSources.Select(entry => entry.Glob)));
    }

    [Fact]
    public void Should_read_more_than_one_glob_under_ignore_diagnostics()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read(
            """{ "ignoreDiagnostics": { "One.he.json": ["LOC0113"], "Two.he.json": ["LOC0207"] } }""");

        // Assert
        Assert.Equal(["One.he.json", "Two.he.json"], document.IgnoreDiagnostics.Select(entry => entry.Glob));
    }

    [Fact]
    public void Should_skip_a_comment_in_the_file()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read(
            """
            {
              // ignore the showcase page's missing translation on purpose
              "ignoreDiagnostics": { "Widget.he.json": ["LOC0113"] }
            }
            """);

        // Assert
        Assert.Single(document.IgnoreDiagnostics);
    }

    [Fact]
    public void Should_allow_a_trailing_comma()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "excludeFiles": ["*/Fixtures/*"], }""");

        // Assert
        Assert.Single(document.ExcludeFiles);
    }

    [Fact]
    public void Should_report_a_top_level_that_is_not_an_object()
    {
        // Act
        var read = LocaliConfigFile.TryRead(".locali.json", Encoding.UTF8.GetBytes("[]"), out var document, out var error);

        // Assert
        Assert.Multiple(
            () => Assert.False(read),
            () => Assert.Null(document),
            () => Assert.NotNull(error));
    }

    [Fact]
    public void Should_report_text_that_is_not_readable_json()
    {
        // Act
        var read = LocaliConfigFile.TryRead(".locali.json", Encoding.UTF8.GetBytes("{ not json"), out var document, out var error);

        // Assert
        Assert.Multiple(
            () => Assert.False(read),
            () => Assert.Null(document),
            () => Assert.NotNull(error));
    }

    [Fact]
    public void Should_flag_an_unknown_top_level_entry()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "included": [] }""");

        // Assert
        var malformedValue = Assert.Single(document.MalformedValues);
        Assert.Contains("'included'", malformedValue.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_flag_an_ignore_diagnostics_entry_that_is_not_an_object()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "ignoreDiagnostics": ["LOC0113"] }""");

        // Assert
        var malformedValue = Assert.Single(document.MalformedValues);
        Assert.Contains("'ignoreDiagnostics'", malformedValue.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_flag_an_exclude_files_entry_that_is_not_an_array()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "excludeFiles": "*/Fixtures/*" }""");

        // Assert
        var malformedValue = Assert.Single(document.MalformedValues);
        Assert.Contains("'excludeFiles'", malformedValue.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_flag_a_glob_whose_rules_are_not_a_list()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "ignoreDiagnostics": { "Widget.he.json": "LOC0113" } }""");

        // Assert
        Assert.Empty(document.IgnoreDiagnostics);
        var malformedValue = Assert.Single(document.MalformedValues);
        Assert.Contains("'Widget.he.json'", malformedValue.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_flag_a_rule_identifier_that_is_not_text()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "ignoreDiagnostics": { "Widget.he.json": ["LOC0113", 5] } }""");

        // Assert
        var entry = Assert.Single(document.IgnoreDiagnostics);
        Assert.Equal(["LOC0113"], entry.Entries.Select(diagnostic => diagnostic.Id));
        Assert.Single(document.MalformedValues);
    }

    [Fact]
    public void Should_flag_a_glob_that_is_not_text()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "excludeFiles": ["*/Fixtures/*", 5] }""");

        // Assert
        Assert.Equal(["*/Fixtures/*"], document.ExcludeFiles.Select(entry => entry.Glob));
        Assert.Single(document.MalformedValues);
    }

    [Fact]
    public void Should_recover_after_a_malformed_entry_to_read_what_follows()
    {
        // Arrange — a stray key in the middle should not stop the ignore entry after it from being read.
        var document = LocaliConfigFileTestFactory.Read(
            """{ "stray": 1, "ignoreDiagnostics": { "Widget.he.json": ["LOC0113"] } }""");

        // Assert
        Assert.Multiple(
            () => Assert.Single(document.MalformedValues),
            () => Assert.Equal(["Widget.he.json"], document.IgnoreDiagnostics.Select(entry => entry.Glob)));
    }

    [Fact]
    public void Should_name_every_entry_the_file_defines_when_one_is_unknown()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "layout": [] }""");

        // Assert
        var malformedValue = Assert.Single(document.MalformedValues);

        Assert.All(
            ["'ignoreDiagnostics'", "'excludeFiles'"],
            key => Assert.Contains(key, malformedValue.Reason, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_locate_a_diagnostic_at_the_offset_given()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read(
            """
            {
              "ignoreDiagnostics": { "Widget.he.json": ["LOC0113"] }
            }
            """);

        var entry = Assert.Single(document.IgnoreDiagnostics);

        // Act
        var diagnostic = document.Diagnose(entry.Offset, LintingCodes.StaleConfigurationEntry, "test");

        // Assert
        Assert.Equal(2, diagnostic.Line);
    }
}
