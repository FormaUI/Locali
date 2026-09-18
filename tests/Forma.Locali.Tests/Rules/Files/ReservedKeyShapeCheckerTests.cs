namespace Forma.Locali.Tests.Rules.Files;

public sealed class ReservedKeyShapeCheckerTests
{
    [Theory]
    [InlineData("$import")]
    [InlineData("$shraed")]
    [InlineData("$x")]
    public void Should_report_a_key_shaped_like_a_reserved_entry(string key)
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithTranslation(key, "משהו"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintingCodes.ReservedShapedKey], diagnostics.Codes());
    }

    [Theory]
    [InlineData("$")]
    [InlineData("$ per month")]
    [InlineData("$5 off")]
    [InlineData("$0.00")]
    [InlineData("$USD")]
    [InlineData("$camelCase")]
    [InlineData("$_private")]
    [InlineData("$tools.x")]
    [InlineData("$import something")]
    [InlineData("$price/month")]
    [InlineData("Costs $5")]
    public void Should_leave_a_key_that_is_not_one_lowercase_word_alone(string key)
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithTranslation(key, "משהו"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("$$price")]
    [InlineData("$$import")]
    public void Should_leave_an_escaped_key_alone(string key)
    {
        // Arrange — a doubled sign keeps the rest as text.
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithTranslation(key, "משהו"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_section_shaped_like_a_reserved_entry()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithSection("$shraed", "Save", "שמור"));

        // Act
        var diagnostic = Assert.Single(SchemaLinterTestFactory.Create().Lint(document, "he"));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.ReservedShapedKey, diagnostic.Code),
            () => Assert.StartsWith("Section '$shraed'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_report_a_section_once_however_many_entries_it_holds()
    {
        // Arrange
        var content = """
            {
              "$localization": "SchemaVersion: 1.0.0; Culture: he;",
              "$shraed": { "Save": "שמור", "Cancel": "בטל", "Nested": { "Close": "סגור" } }
            }
            """;
        var document = LocalizationDocumentTestFactory.Read(LocalizationFileFakeData.HebrewName, content);

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintingCodes.ReservedShapedKey], diagnostics.Codes());
    }

    [Fact]
    public void Should_report_a_shaped_key_inside_a_section()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithSection("Widget", "$import", "ייבוא"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintingCodes.ReservedShapedKey], diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_the_reserved_entries_the_runtime_defines_alone()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithReservedOrder("$localization", "$shared", "Widget"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.DoesNotContain(LintingCodes.ReservedShapedKey, diagnostics.Codes());
    }
}
