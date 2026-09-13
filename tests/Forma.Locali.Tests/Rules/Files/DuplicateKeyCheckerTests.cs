namespace Forma.Locali.Tests.Rules.Files;

public sealed class DuplicateKeyCheckerTests
{
    [Fact]
    public void Should_report_a_key_the_file_declares_twice()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithDuplicateKey("Dismiss"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintCodes.DuplicateKey], diagnostics.Codes());
    }

    [Fact]
    public void Should_let_a_key_repeat_across_sections()
    {
        // Arrange — two types sharing a string each declare it in their own section.
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithSections(("Widget", "Dismiss", "סגור"), ("Gallery", "Dismiss", "סגור")));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_key_a_section_declares_twice()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            """{ "$localization": "SchemaVersion: 1.0.0; Culture: he;", "Widget": { "Dismiss": "ראשון", "Dismiss": "אחרון" } }""");

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintCodes.DuplicateKey], diagnostics.Codes());
    }

    [Fact]
    public void Should_report_the_later_declaration_rather_than_the_first()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithDuplicateKey("Dismiss"));

        // Act
        var diagnostic = Assert.Single(SchemaLinterTestFactory.Create().Lint(document, "he"));

        // Assert
        Assert.Equal(5, diagnostic.Line);
    }

    [Fact]
    public void Should_report_once_for_each_declaration_beyond_the_first()
    {
        // Arrange
        var content = $$"""
            {
              "$localization": "SchemaVersion: 1.0.0; Culture: he;",
              "Dismiss": "ראשון",
              "Dismiss": "שני",
              "Dismiss": "שלישי"
            }
            """;
        var document = LocalizationDocumentTestFactory.Read(LocalizationFileFakeData.HebrewName, content);

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintCodes.DuplicateKey, LintCodes.DuplicateKey], diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_a_file_declaring_each_key_once_alone()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_tell_two_keys_differing_only_by_case_apart()
    {
        // Arrange
        var content = $$"""
            {
              "$localization": "SchemaVersion: 1.0.0; Culture: he;",
              "Dismiss": "ראשון",
              "dismiss": "שני"
            }
            """;
        var document = LocalizationDocumentTestFactory.Read(LocalizationFileFakeData.HebrewName, content);

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }
}
