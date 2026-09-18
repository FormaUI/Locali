namespace Forma.Locali.Tests.Rules.Files;

public sealed class SharedEntryCheckerTests
{
    [Theory]
    [InlineData("\"text\"")]
    [InlineData("null")]
    [InlineData("[\"Save\"]")]
    [InlineData("1")]
    public void Should_report_a_shared_entry_that_is_not_an_object(string value)
    {
        // Arrange
        var content = $$"""
            {
              "$localization": "SchemaVersion: 1.0.0; Culture: he;",
              "$shared": {{value}}
            }
            """;
        var document = LocalizationDocumentTestFactory.Read(LocalizationFileFakeData.HebrewName, content);

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintingCodes.MalformedShared], diagnostics.Codes());
    }

    [Fact]
    public void Should_report_a_shared_entry_inside_a_section_that_is_not_an_object()
    {
        // Arrange
        var content = """
            {
              "$localization": "SchemaVersion: 1.0.0; Culture: he;",
              "Widget": { "$shared": "text", "Today": "היום" }
            }
            """;
        var document = LocalizationDocumentTestFactory.Read(LocalizationFileFakeData.HebrewName, content);

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintingCodes.MalformedShared], diagnostics.Codes());
    }

    [Fact]
    public void Should_report_a_section_nested_inside_a_shared_section()
    {
        // Arrange
        var content = """
            {
              "$localization": "SchemaVersion: 1.0.0; Culture: he;",
              "$shared": { "Save": "שמור", "Sub": { "Close": "סגור" } }
            }
            """;
        var document = LocalizationDocumentTestFactory.Read(LocalizationFileFakeData.HebrewName, content);

        // Act
        var diagnostic = Assert.Single(SchemaLinterTestFactory.Create().Lint(document, "he"));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.MalformedShared, diagnostic.Code),
            () => Assert.Contains("nests 'Sub'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_report_a_shared_section_nested_inside_another()
    {
        // Arrange
        var content = """
            {
              "$localization": "SchemaVersion: 1.0.0; Culture: he;",
              "$shared": { "$shared": { "Open": "פתח" } }
            }
            """;
        var document = LocalizationDocumentTestFactory.Read(LocalizationFileFakeData.HebrewName, content);

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintingCodes.MalformedShared], diagnostics.Codes());
    }

    [Fact]
    public void Should_report_a_nested_section_once_however_many_entries_it_holds()
    {
        // Arrange
        var content = """
            {
              "$localization": "SchemaVersion: 1.0.0; Culture: he;",
              "$shared": { "Sub": { "Close": "סגור", "Open": "פתח", "Deeper": { "Save": "שמור" } } }
            }
            """;
        var document = LocalizationDocumentTestFactory.Read(LocalizationFileFakeData.HebrewName, content);

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintingCodes.MalformedShared], diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_a_shared_section_holding_translations_alone_alone()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithReservedOrder("$localization", "$shared", "Widget"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_shared_section_inside_a_section_alone()
    {
        // Arrange
        var content = """
            {
              "$localization": "SchemaVersion: 1.0.0; Culture: he;",
              "Widget": { "$shared": { "Save": "שמור" }, "Today": "היום" }
            }
            """;
        var document = LocalizationDocumentTestFactory.Read(LocalizationFileFakeData.HebrewName, content);

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }
}
