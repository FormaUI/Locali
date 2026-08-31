namespace Forma.Locali.Tests.Rules.Files;

public sealed class TranslationHolesCheckerTests
{
    [Fact]
    public void Should_report_a_translation_filling_a_hole_the_key_does_not_name()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithTranslation("Stored {stored} of {total}.", "מתוך {total} נשמרו {totl}."));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintCodes.UnknownTranslationHole], diagnostics.Codes());
    }

    [Fact]
    public void Should_accept_a_translation_that_reorders_the_key_s_holes()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithTranslation("Stored {stored} of {total}.", "מתוך {total} נשמרו {stored}."));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_accept_a_translation_that_drops_a_hole()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithTranslation("Stored {stored} of {total}.", "נשמרו {stored}."));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_accept_a_translation_that_is_deliberately_the_key()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithNullTranslation("Stored {stored} of {total}."));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_not_read_the_metadata_entry_as_a_translation()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithBracedMarker);

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_accept_a_translation_naming_the_key_s_hole_as_a_plural_argument()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithTranslation(
                "{selected} selected",
                "[$ [selected] plural; one: נבחר אחד; other: # נבחרו; $]"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_plural_argument_the_key_does_not_name()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithTranslation(
                "{selected} selected",
                "[$ [selectd] plural; one: נבחר אחד; other: # נבחרו; $]"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintCodes.UnknownTranslationHole], diagnostics.Codes());
    }

    [Fact]
    public void Should_accept_a_translation_whose_key_names_its_hole_as_a_plural_argument()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithTranslation(
                "[$ [count] plural; one: # file; other: # files; $]",
                "{count} קבצים"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_each_unknown_hole_once_however_often_it_appears()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithTranslation("Stored {stored}.", "{totl} נשמרו {totl}."));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintCodes.UnknownTranslationHole], diagnostics.Codes());
    }

    [Fact]
    public void Should_accept_escaped_braces_as_literal_text()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithTranslation("Use {{curly}} braces.", "השתמשו בסוגריים {{מסולסלים}}."));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_point_at_the_translation_that_carries_the_hole()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithTranslation("Stored {stored} of {total}.", "נשמרו {totl}."));

        // Act
        var diagnostic = SchemaLinterTestFactory.Create().Lint(document, "he").Single();

        // Assert
        Assert.Equal(4, diagnostic.Line);
        Assert.Equal(34, diagnostic.Column);
    }
}
