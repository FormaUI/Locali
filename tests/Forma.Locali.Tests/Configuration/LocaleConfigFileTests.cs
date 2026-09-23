namespace Forma.Locali.Tests.Configuration;

using System.Text;

using Forma.Locali.Configuration;

public sealed class LocaleConfigFileTests
{
    [Theory]
    [InlineData("""{ "rootNamespace": "Contoso.Widgets" }""")]
    [InlineData("""{ "rootDir": "Strings/Main" }""")]
    [InlineData("""{ "localesDir": ".locales" }""")]
    [InlineData("""{ "paths": ["$RootDir/$AnchorName.$Culture$Extension"] }""")]
    [InlineData("{}")]
    public void Should_read_a_key_the_runtime_reads_without_complaint(string json)
        => Assert.Empty(LocaleConfigFileTestFactory.Read(json).MalformedValues);

    [Theory]
    [InlineData("""{ "rootNamespace": "Contoso..Widgets" }""", "not a namespace")]
    [InlineData("""{ "rootNamespace": 5 }""", "'rootNamespace' is not text")]
    [InlineData("""{ "rootDir": "/strings" }""", "not a relative folder")]
    [InlineData("""{ "localesDir": "../strings" }""", "not a relative folder")]
    [InlineData("""{ "paths": "$RootDir/$AnchorName.$Culture$Extension" }""", "'paths' is not a list of text")]
    [InlineData("""{ "paths": [5] }""", "a path is not text")]
    [InlineData("""{ "paths": ["$RootDir/$AnchorName.json"] }""", "'$Culture' and '$Extension'")]
    [InlineData("""{ "paths": [] }""", "'paths' lists no path")]
    [InlineData("""{ "localesDir": ".locales", "localesDir": "strings" }""", "'localesDir' is written more than once")]
    public void Should_flag_a_value_the_runtime_refuses(string json, string expected)
    {
        // Arrange
        var file = LocaleConfigFileTestFactory.Read(json);

        // Assert
        var malformedValue = Assert.Single(file.MalformedValues);
        Assert.Contains(expected, malformedValue.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_flag_a_key_the_runtime_does_not_read_naming_the_ones_it_does()
    {
        // Arrange — the runtime passes such a key over, so a misspelled one steers nothing.
        var file = LocaleConfigFileTestFactory.Read("""{ "rootNamespce": "Contoso.Widgets" }""");

        // Assert
        var malformedValue = Assert.Single(file.MalformedValues);

        Assert.All(
            ["'rootNamespce'", "'rootNamespace'", "'rootDir'", "'localesDir'", "'paths'"],
            key => Assert.Contains(key, malformedValue.Reason, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_flag_a_malformed_path_where_its_token_is_written()
    {
        // Arrange
        var file = LocaleConfigFileTestFactory.Read(
            """
            {
              "paths": ["$RootDir/$Folder.$Culture$Extension"]
            }
            """);

        var malformedValue = Assert.Single(file.MalformedValues);

        // Act
        var diagnostic = file.Diagnose(malformedValue.Offset, LintingCodes.MalformedLocaleConfig, malformedValue.Reason);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Equal(23, diagnostic.Column),
            () => Assert.Contains("'$Folder'", malformedValue.Reason, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_flag_a_malformed_path_where_its_token_is_written_after_text_beyond_ascii()
    {
        // Arrange
        var file = LocaleConfigFileTestFactory.Read(
            """
            {
              "paths": ["$RootDir/תרגום/$Folder.$Culture$Extension"]
            }
            """);

        var malformedValue = Assert.Single(file.MalformedValues);

        // Act
        var diagnostic = file.Diagnose(malformedValue.Offset, LintingCodes.MalformedLocaleConfig, malformedValue.Reason);

        // Assert
        Assert.Equal(29, diagnostic.Column);
    }

    [Fact]
    public void Should_report_why_a_file_is_not_readable()
    {
        // Act
        var read = LocaleConfigFile.TryRead(".locale.json", Encoding.UTF8.GetBytes("[]"), out var file, out var error);

        // Assert
        Assert.Multiple(
            () => Assert.False(read),
            () => Assert.Null(file),
            () => Assert.Contains("not a JSON object", error, StringComparison.Ordinal));
    }
}
