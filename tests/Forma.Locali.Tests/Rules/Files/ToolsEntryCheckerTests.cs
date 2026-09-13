namespace Forma.Locali.Tests.Rules.Files;

public sealed class ToolsEntryCheckerTests
{
    [Fact]
    public void Should_leave_an_ignore_list_naming_rules_alone()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithIgnore(["LOC0113", "LOC0111"], ("Widget", "Today", "היום")));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.DoesNotContain(LintCodes.MalformedTools, diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_another_tools_entries_alone()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            """{ "$localization": "SchemaVersion: 1.0.0; Culture: he;", "$tools": { "crowdin.status": "reviewed", "crowdin": { "id": 12 } }, "Widget": { "Today": "היום" } }""");

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.DoesNotContain(LintCodes.MalformedTools, diagnostics.Codes());
    }

    [Fact]
    public void Should_report_a_name_that_is_no_rule()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithIgnore(["LOC0113", "hole shape"], ("Widget", "Today", "היום")));

        // Act
        var diagnostic = Assert.Single(SchemaLinterTestFactory.Create().Lint(document, "he"), diagnostic => diagnostic.Code == LintCodes.MalformedTools);

        // Assert
        Assert.Contains("'hole shape'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_report_an_ignore_entry_that_is_not_a_list()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            """{ "$localization": "SchemaVersion: 1.0.0; Culture: he;", "$tools": { "locali.ignore": "LOC0113" }, "Widget": { "Today": "היום" } }""");

        // Act
        var diagnostic = Assert.Single(SchemaLinterTestFactory.Create().Lint(document, "he"), diagnostic => diagnostic.Code == LintCodes.MalformedTools);

        // Assert
        Assert.Contains("not a list", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_report_an_entry_of_this_linters_it_does_not_define()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            """{ "$localization": "SchemaVersion: 1.0.0; Culture: he;", "$tools": { "locali.ignores": ["LOC0113"] }, "Widget": { "Today": "היום" } }""");

        // Act
        var diagnostic = Assert.Single(SchemaLinterTestFactory.Create().Lint(document, "he"), diagnostic => diagnostic.Code == LintCodes.MalformedTools);

        // Assert
        Assert.Contains("'locali.ignores'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_report_this_linters_entries_nested_in_an_object()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            """{ "$localization": "SchemaVersion: 1.0.0; Culture: he;", "$tools": { "locali": { "ignore": ["LOC0113"] } }, "Widget": { "Today": "היום" } }""");

        // Act
        var diagnostic = Assert.Single(SchemaLinterTestFactory.Create().Lint(document, "he"), diagnostic => diagnostic.Code == LintCodes.MalformedTools);

        // Assert
        Assert.Contains("'locali.ignore'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_report_a_tools_entry_that_is_not_an_object()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            """{ "$localization": "SchemaVersion: 1.0.0; Culture: he;", "$tools": ["LOC0113"], "Widget": { "Today": "היום" } }""");

        // Act
        var diagnostic = Assert.Single(SchemaLinterTestFactory.Create().Lint(document, "he"), diagnostic => diagnostic.Code == LintCodes.MalformedTools);

        // Assert
        Assert.Contains("not an object", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_report_a_tools_entry_inside_a_section()
    {
        // Arrange — the runtime drops the entry wherever it sits, and only the top-level one is read.
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            """{ "$localization": "SchemaVersion: 1.0.0; Culture: he;", "Widget": { "$tools": { "locali.ignore": ["LOC0113"] }, "Today": "היום" } }""");

        // Act
        var diagnostic = Assert.Single(SchemaLinterTestFactory.Create().Lint(document, "he"), diagnostic => diagnostic.Code == LintCodes.MalformedTools);

        // Assert
        Assert.Contains("under 'Widget'", diagnostic.Message, StringComparison.Ordinal);
    }
}
