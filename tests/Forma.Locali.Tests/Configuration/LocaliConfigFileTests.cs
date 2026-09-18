namespace Forma.Locali.Tests.Configuration;

using System.Text;

using Forma.Locali.Configuration;

public sealed class LocaliConfigFileTests
{
    [Fact]
    public void Should_read_a_globs_codes_with_their_offsets()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "ignoreDiagnostics": { "**/LocalizationPage.razor": ["LOC0209", "LOC0113"] } }""");

        // Assert
        var entry = Assert.Single(document.IgnoreDiagnostics);

        Assert.Multiple(
            () => Assert.Equal("**/LocalizationPage.razor", entry.Glob),
            () => Assert.Equal(["LOC0209", "LOC0113"], entry.Codes.Select(code => code.Id)));
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
    public void Should_read_more_than_one_glob_under_ignore_diagnostics()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read(
            """{ "ignoreDiagnostics": { "One.he.json": ["LOC0113"], "Two.he.json": ["LOC0209"] } }""");

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
        var malformed = Assert.Single(document.Malformed);
        Assert.Contains("'included'", malformed.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_flag_an_ignore_diagnostics_entry_that_is_not_an_object()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "ignoreDiagnostics": ["LOC0113"] }""");

        // Assert
        var malformed = Assert.Single(document.Malformed);
        Assert.Contains("'ignoreDiagnostics'", malformed.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_flag_an_exclude_files_entry_that_is_not_an_array()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "excludeFiles": "*/Fixtures/*" }""");

        // Assert
        var malformed = Assert.Single(document.Malformed);
        Assert.Contains("'excludeFiles'", malformed.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_flag_a_glob_whose_rules_are_not_a_list()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "ignoreDiagnostics": { "Widget.he.json": "LOC0113" } }""");

        // Assert
        Assert.Empty(document.IgnoreDiagnostics);
        var malformed = Assert.Single(document.Malformed);
        Assert.Contains("'Widget.he.json'", malformed.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_flag_a_rule_identifier_that_is_not_text()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "ignoreDiagnostics": { "Widget.he.json": ["LOC0113", 5] } }""");

        // Assert
        var entry = Assert.Single(document.IgnoreDiagnostics);
        Assert.Equal(["LOC0113"], entry.Codes.Select(code => code.Id));
        Assert.Single(document.Malformed);
    }

    [Fact]
    public void Should_flag_a_glob_that_is_not_text()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "excludeFiles": ["*/Fixtures/*", 5] }""");

        // Assert
        Assert.Equal(["*/Fixtures/*"], document.ExcludeFiles.Select(entry => entry.Glob));
        Assert.Single(document.Malformed);
    }

    [Fact]
    public void Should_recover_after_a_malformed_entry_to_read_what_follows()
    {
        // Arrange — a stray key in the middle should not stop the ignore entry after it from being read.
        var document = LocaliConfigFileTestFactory.Read(
            """{ "stray": 1, "ignoreDiagnostics": { "Widget.he.json": ["LOC0113"] } }""");

        // Assert
        Assert.Multiple(
            () => Assert.Single(document.Malformed),
            () => Assert.Equal(["Widget.he.json"], document.IgnoreDiagnostics.Select(entry => entry.Glob)));
    }

    [Fact]
    public void Should_name_every_entry_the_file_defines_when_one_is_unknown()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "layout": [] }""");

        // Assert
        var malformed = Assert.Single(document.Malformed);

        Assert.All(
            ["'ignoreDiagnostics'", "'excludeFiles'"],
            key => Assert.Contains(key, malformed.Reason, StringComparison.Ordinal));
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
        var diagnostic = document.Diagnose(entry.Offset, LintingCodes.StaleConfigurationGlob, "test");

        // Assert
        Assert.Equal(2, diagnostic.Line);
    }
}
