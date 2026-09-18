namespace Forma.Locali.Tests.Configuration;

using System.Text;

using Forma.Locali.Configuration;

public sealed class LocaliConfigFileTests
{
    [Fact]
    public void Should_read_a_globs_codes_with_their_offsets()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "ignore": { "**/LocalizationPage.razor": ["LOC0209", "LOC0113"] } }""");

        // Assert
        var entry = Assert.Single(document.IgnoreEntries);

        Assert.Multiple(
            () => Assert.Equal("**/LocalizationPage.razor", entry.Glob),
            () => Assert.Equal(["LOC0209", "LOC0113"], entry.Codes.Select(code => code.Id)));
    }

    [Fact]
    public void Should_read_an_exclude_globs_with_their_offsets()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "exclude": ["*/Fixtures/*"] }""");

        // Assert
        var entry = Assert.Single(document.ExcludeEntries);

        Assert.Equal("*/Fixtures/*", entry.Glob);
    }

    [Fact]
    public void Should_read_more_than_one_glob_under_ignore()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read(
            """{ "ignore": { "One.he.json": ["LOC0113"], "Two.he.json": ["LOC0209"] } }""");

        // Assert
        Assert.Equal(["One.he.json", "Two.he.json"], document.IgnoreEntries.Select(entry => entry.Glob));
    }

    [Fact]
    public void Should_skip_a_comment_in_the_file()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read(
            """
            {
              // ignore the showcase page's missing translation on purpose
              "ignore": { "Widget.he.json": ["LOC0113"] }
            }
            """);

        // Assert
        Assert.Single(document.IgnoreEntries);
    }

    [Fact]
    public void Should_allow_a_trailing_comma()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "exclude": ["*/Fixtures/*"], }""");

        // Assert
        Assert.Single(document.ExcludeEntries);
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
    public void Should_flag_an_ignore_entry_that_is_not_an_object()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "ignore": ["LOC0113"] }""");

        // Assert
        var malformed = Assert.Single(document.Malformed);
        Assert.Contains("'ignore'", malformed.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_flag_an_exclude_entry_that_is_not_an_array()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "exclude": "*/Fixtures/*" }""");

        // Assert
        var malformed = Assert.Single(document.Malformed);
        Assert.Contains("'exclude'", malformed.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_flag_a_glob_whose_rules_are_not_a_list()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "ignore": { "Widget.he.json": "LOC0113" } }""");

        // Assert
        Assert.Empty(document.IgnoreEntries);
        var malformed = Assert.Single(document.Malformed);
        Assert.Contains("'Widget.he.json'", malformed.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_flag_a_rule_identifier_that_is_not_text()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "ignore": { "Widget.he.json": ["LOC0113", 5] } }""");

        // Assert
        var entry = Assert.Single(document.IgnoreEntries);
        Assert.Equal(["LOC0113"], entry.Codes.Select(code => code.Id));
        Assert.Single(document.Malformed);
    }

    [Fact]
    public void Should_flag_a_glob_that_is_not_text()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "exclude": ["*/Fixtures/*", 5] }""");

        // Assert
        Assert.Equal(["*/Fixtures/*"], document.ExcludeEntries.Select(entry => entry.Glob));
        Assert.Single(document.Malformed);
    }

    [Fact]
    public void Should_recover_after_a_malformed_entry_to_read_what_follows()
    {
        // Arrange — a stray key in the middle should not stop the ignore entry after it from being read.
        var document = LocaliConfigFileTestFactory.Read(
            """{ "stray": 1, "ignore": { "Widget.he.json": ["LOC0113"] } }""");

        // Assert
        Assert.Multiple(
            () => Assert.Single(document.Malformed),
            () => Assert.Equal(["Widget.he.json"], document.IgnoreEntries.Select(entry => entry.Glob)));
    }

    [Theory]
    [InlineData("""{ "rootNamespace": "Contoso.Widgets" }""")]
    [InlineData("""{ "rootDir": "./Contoso.csproj" }""")]
    [InlineData("""{ "localesDir": ".locales" }""")]
    [InlineData("""{ "paths": ["$RootDir/$AnchorName.$Culture$Extension"] }""")]
    public void Should_read_a_key_the_runtime_owns_without_complaint(string json)
        => Assert.Empty(LocaliConfigFileTestFactory.Read(json).Malformed);

    [Theory]
    [InlineData("""{ "rootNamespace": "Contoso..Widgets" }""", "not a namespace")]
    [InlineData("""{ "rootNamespace": 5 }""", "'rootNamespace' is not text")]
    [InlineData("""{ "rootDir": "/strings" }""", "not a relative path")]
    [InlineData("""{ "localesDir": "../strings" }""", "not a relative folder")]
    [InlineData("""{ "paths": "$RootDir/$AnchorName.$Culture$Extension" }""", "'paths' is not a list of text")]
    [InlineData("""{ "paths": [5] }""", "a path is not text")]
    [InlineData("""{ "paths": ["$RootDir/$AnchorName.json"] }""", "'$Culture' and '$Extension'")]
    public void Should_flag_a_key_the_runtime_refuses(string json, string expected)
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read(json);

        // Assert
        var malformed = Assert.Single(document.Malformed);
        Assert.Contains(expected, malformed.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_flag_a_malformed_path_where_its_token_is_written()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read(
            """
            {
              "paths": ["$RootDir/$Folder.$Culture$Extension"]
            }
            """);

        var malformed = Assert.Single(document.Malformed);

        // Act
        var diagnostic = document.Diagnose(malformed.Offset, LintingCodes.MalformedConfiguration, malformed.Reason);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Equal(23, diagnostic.Column),
            () => Assert.Contains("'$Folder'", malformed.Reason, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_flag_a_malformed_path_where_its_token_is_written_after_text_beyond_ascii()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read(
            """
            {
              "paths": ["$RootDir/תרגום/$Folder.$Culture$Extension"]
            }
            """);

        var malformed = Assert.Single(document.Malformed);

        // Act
        var diagnostic = document.Diagnose(malformed.Offset, LintingCodes.MalformedConfiguration, malformed.Reason);

        // Assert
        Assert.Equal(29, diagnostic.Column);
    }

    [Fact]
    public void Should_flag_paths_that_list_nothing()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "paths": [] }""");

        // Assert
        var malformed = Assert.Single(document.Malformed);
        Assert.Contains("'paths' lists no path", malformed.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_name_every_entry_the_file_defines_when_one_is_unknown()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read("""{ "layout": [] }""");

        // Assert
        var malformed = Assert.Single(document.Malformed);

        Assert.All(
            ["'ignore'", "'exclude'", "'rootNamespace'", "'rootDir'", "'localesDir'", "'paths'"],
            key => Assert.Contains(key, malformed.Reason, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_locate_a_diagnostic_at_the_offset_given()
    {
        // Arrange
        var document = LocaliConfigFileTestFactory.Read(
            """
            {
              "ignore": { "Widget.he.json": ["LOC0113"] }
            }
            """);

        var entry = Assert.Single(document.IgnoreEntries);

        // Act
        var diagnostic = document.Diagnose(entry.Offset, LintingCodes.StaleConfigurationGlob, "test");

        // Assert
        Assert.Equal(2, diagnostic.Line);
    }
}
