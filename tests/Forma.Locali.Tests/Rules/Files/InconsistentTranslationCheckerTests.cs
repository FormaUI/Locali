namespace Forma.Locali.Tests.Rules.Files;

public sealed class InconsistentTranslationCheckerTests
{
    [Fact]
    public void Should_report_a_key_two_sections_translate_differently()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithSections(("Widget", "Font Family", "גופן"), ("Gallery", "Font Family", "משפחת גופנים")));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintingCodes.InconsistentTranslation], diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_a_key_two_sections_translate_alike_alone()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithSections(("Widget", "Dismiss", "סגור"), ("Gallery", "Dismiss", "סגור")));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_two_sections_translating_different_keys_alone()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithSections(("Widget", "Dismiss", "סגור"), ("Gallery", "Close", "סגירה")));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_the_later_section_rather_than_the_first()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithSections(("Widget", "Font Family", "גופן"), ("Gallery", "Font Family", "משפחת גופנים")));

        // Act
        var diagnostic = Assert.Single(SchemaLinterTestFactory.Create().Lint(document, "he"));

        // Assert
        Assert.Equal(4, diagnostic.Line);
    }

    [Fact]
    public void Should_name_the_section_the_translation_differs_from()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithSections(("Widget", "Font Family", "גופן"), ("Gallery", "Font Family", "משפחת גופנים")));

        // Act
        var diagnostic = Assert.Single(SchemaLinterTestFactory.Create().Lint(document, "he"));

        // Assert
        Assert.Equal(
            "The key 'Font Family' is translated as 'משפחת גופנים' here and as 'גופן' under 'Widget', so the same English text reads two ways at run time.",
            diagnostic.Message);
    }

    [Fact]
    public void Should_report_once_for_each_section_that_differs_from_the_first()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithSections(
                ("Widget", "Font Family", "גופן"),
                ("Gallery", "Font Family", "משפחת גופנים"),
                ("Composer", "Font Family", "גופן"),
                ("Editor", "Font Family", "סוג גופן")));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintingCodes.InconsistentTranslation, LintingCodes.InconsistentTranslation], diagnostics.Codes());
    }

    [Fact]
    public void Should_read_a_null_translation_as_the_key_text()
    {
        // Arrange — one section leaves the key as written, the other writes the key text out.
        var content = """
            {
              "$localization": "SchemaVersion: 1.0.0; Culture: he;",
              "Widget": { "OK": null },
              "Gallery": { "OK": "OK" }
            }
            """;
        var document = LocalizationDocumentTestFactory.Read(LocalizationFileFakeData.HebrewName, content);

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_null_translation_against_one_that_differs_from_the_key()
    {
        // Arrange
        var content = """
            {
              "$localization": "SchemaVersion: 1.0.0; Culture: he;",
              "Widget": { "OK": null },
              "Gallery": { "OK": "אישור" }
            }
            """;
        var document = LocalizationDocumentTestFactory.Read(LocalizationFileFakeData.HebrewName, content);

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintingCodes.InconsistentTranslation], diagnostics.Codes());
    }

    [Fact]
    public void Should_compare_a_top_level_entry_with_a_section()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithSectionAndStray("Widget", "Dismiss", "סגור", "Dismiss", "בטל"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintingCodes.InconsistentTranslation], diagnostics.Codes());
    }

    [Fact]
    public void Should_name_the_top_level_when_the_first_declaration_sits_there()
    {
        // Arrange
        var content = """
            {
              "$localization": "SchemaVersion: 1.0.0; Culture: he;",
              "Dismiss": "בטל",
              "Widget": { "Dismiss": "סגור" }
            }
            """;
        var document = LocalizationDocumentTestFactory.Read(LocalizationFileFakeData.HebrewName, content);

        // Act
        var diagnostic = Assert.Single(SchemaLinterTestFactory.Create().Lint(document, "he"));

        // Assert
        Assert.Equal(
            "The key 'Dismiss' is translated as 'סגור' here and as 'בטל' at the file's top level, so the same English text reads two ways at run time.",
            diagnostic.Message);
    }

    [Fact]
    public void Should_compare_the_last_declaration_a_section_makes()
    {
        // Arrange — the section's first declaration disagrees, but the runtime keeps its last, which agrees.
        var content = """
            {
              "$localization": "SchemaVersion: 1.0.0; Culture: he;",
              "Widget": { "Dismiss": "בטל", "Dismiss": "סגור" },
              "Gallery": { "Dismiss": "סגור" }
            }
            """;
        var document = LocalizationDocumentTestFactory.Read(LocalizationFileFakeData.HebrewName, content);

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintingCodes.DuplicateKey], diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_a_key_declared_twice_in_one_section_to_the_duplicate_rule()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithDuplicateKey("Dismiss"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintingCodes.DuplicateKey], diagnostics.Codes());
    }

    [Fact]
    public void Should_tell_two_keys_differing_only_by_case_apart()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithSections(("Widget", "Dismiss", "סגור"), ("Gallery", "dismiss", "בטל")));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_another_tools_entries_alone()
    {
        // Arrange — the runtime never reads what sits under the tools entry, so it is no translation.
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            """{ "$localization": "SchemaVersion: 1.0.0; Culture: he;", "$tools": { "crowdin.label": "Bold" }, "Widget": { "crowdin.label": "מודגש" } }""");

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }
}
