namespace Forma.Locali.Tests.Linting;

public sealed class SchemaLinterTests
{
    [Fact]
    public void Should_report_a_marker_that_is_not_the_first_entry()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithTrailingMarker("SchemaVersion: 1.0.0; Culture: he;"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintCodes.MarkerNotFirst], diagnostics.Codes());
    }

    [Fact]
    public void Should_treat_a_marker_out_of_first_place_as_a_warning()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithTrailingMarker("SchemaVersion: 1.0.0; Culture: he;"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal(LintSeverity.Warning, diagnostics.SoleSeverity());
    }

    [Fact]
    public void Should_report_a_malformed_marker()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithMarker("SchemaVersion 1.0.0"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintCodes.MalformedMetadata], diagnostics.Codes());
    }

    [Fact]
    public void Should_report_a_missing_required_field()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithMarker("SchemaVersion: 1.0.0;"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintCodes.MissingRequiredField], diagnostics.Codes());
    }

    [Fact]
    public void Should_report_required_fields_written_out_of_order()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithMarker("Culture: he; SchemaVersion: 1.0.0;"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintCodes.RequiredFieldOutOfOrder], diagnostics.Codes());
    }

    [Fact]
    public void Should_treat_required_fields_out_of_order_as_a_warning()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithMarker("Culture: he; SchemaVersion: 1.0.0;"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal(LintSeverity.Warning, diagnostics.SoleSeverity());
    }

    [Fact]
    public void Should_report_a_schema_version_this_build_does_not_define()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithMarker("SchemaVersion: 1.1.0; Culture: he;"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintCodes.SchemaVersionMismatch], diagnostics.Codes());
    }

    [Fact]
    public void Should_treat_a_differing_minor_version_as_a_warning()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithMarker("SchemaVersion: 1.1.0; Culture: he;"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal(LintSeverity.Warning, diagnostics.SoleSeverity());
    }

    [Fact]
    public void Should_treat_a_differing_major_version_as_an_error()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithMarker("SchemaVersion: 2.0.0; Culture: he;"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal(LintSeverity.Error, diagnostics.SoleSeverity());
    }

    [Fact]
    public void Should_report_a_field_the_schema_does_not_define()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithMarker("SchemaVersion: 1.0.0; Culture: he; Translator: Dana;"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintCodes.UnknownMetadataField], diagnostics.Codes());
    }

    [Fact]
    public void Should_treat_an_unknown_field_as_a_warning()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithMarker("SchemaVersion: 1.0.0; Culture: he; Translator: Dana;"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal(LintSeverity.Warning, diagnostics.SoleSeverity());
    }

    [Fact]
    public void Should_accept_the_optional_fields()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithMarker("SchemaVersion: 1.0.0; Culture: he; Author: Dana; Revision: 3;"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_culture_the_file_name_does_not_give_it()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithMarker("SchemaVersion: 1.0.0; Culture: es;"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal([LintCodes.CultureMismatch], diagnostics.Codes());
    }

    [Fact]
    public void Should_treat_a_culture_mismatch_as_an_error()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithMarker("SchemaVersion: 1.0.0; Culture: es;"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal(LintSeverity.Error, diagnostics.SoleSeverity());
    }

    [Fact]
    public void Should_point_at_the_line_the_marker_sits_on()
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(
            LocalizationFileFakeData.HebrewName,
            LocalizationFileFakeData.WithMarker("SchemaVersion: 1.0.0; Culture: es;"));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Equal(2, diagnostics.Single().Line);
    }

    [Fact]
    public void Should_write_a_diagnostic_an_editor_can_navigate_to()
    {
        // Arrange
        var diagnostic = new LintDiagnostic("C:/x/Colocated.he.json", 2, 20, LintSeverity.Error, LintCodes.CultureMismatch, "Wrong.");

        // Act
        var written = diagnostic.ToString();

        // Assert
        Assert.Equal("C:/x/Colocated.he.json(2,20): error LOC0108: Wrong.", written);
    }
}
