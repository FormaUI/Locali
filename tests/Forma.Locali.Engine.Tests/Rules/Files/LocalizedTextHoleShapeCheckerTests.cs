namespace Forma.Locali.Tests.Rules.Files;

public sealed class LocalizedTextHoleShapeCheckerTests
{
    [Theory]
    [InlineData("נשמרו {0}")]
    [InlineData("נשמרו {stored.Value}")]
    public void Should_report_a_text_writing_something_shaped_like_a_hole(string text)
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithLocalizationEntry("Stored {stored}.", text));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintingCodes.LocalizedTextHoleShapedText], diagnostics.Codes());
    }

    [Fact]
    public void Should_report_a_text_inside_a_section()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithSection("Widget", "Stored {stored}.", "נשמרו {0}"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintingCodes.LocalizedTextHoleShapedText], diagnostics.Codes());
    }

    [Theory]
    [InlineData("נשמרו {stored}.")]
    [InlineData("השתמשו ב {{curly}}")]
    [InlineData("body { color: red }")]
    public void Should_leave_a_text_the_runtime_reads_as_written_alone(string text)
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithLocalizationEntry("Stored {stored}.", text));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }
}
