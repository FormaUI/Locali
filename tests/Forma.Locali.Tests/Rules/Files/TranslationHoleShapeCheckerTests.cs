namespace Forma.Locali.Tests.Rules.Files;

public sealed class TranslationHoleShapeCheckerTests
{
    [Theory]
    [InlineData("נשמרו {0}")]
    [InlineData("נשמרו {stored.Value}")]
    public void Should_report_a_translation_writing_something_shaped_like_a_hole(string translation)
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithTranslation("Stored {stored}.", translation));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintCodes.TranslationHoleShapedText], diagnostics.Codes());
    }

    [Theory]
    [InlineData("נשמרו {stored}.")]
    [InlineData("השתמשו ב {{curly}}")]
    [InlineData("body { color: red }")]
    public void Should_leave_a_translation_the_runtime_reads_as_written_alone(string translation)
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithTranslation("Stored {stored}.", translation));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }
}
