namespace Forma.Locali.Tests.Rules.Files;

public sealed class ReservedEntryOrderCheckerTests
{
    [Theory]
    [InlineData("$localization", "$tools", "$shared", "Widget")]
    [InlineData("$localization", "$shared", "Widget")]
    [InlineData("$localization", "$tools", "Widget")]
    [InlineData("$localization", "Widget")]
    public void Should_leave_reserved_entries_in_their_order_alone(params string[] order)
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.WithReservedOrder(order));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.DoesNotContain(LintingCodes.ReservedEntryOutOfOrder, diagnostics.Codes());
    }

    [Theory]
    [InlineData("$localization", "$shared", "$tools", "Widget")]
    [InlineData("$localization", "Widget", "$shared")]
    [InlineData("$localization", "Widget", "$tools")]
    public void Should_report_a_reserved_entry_after_one_it_has_to_precede(params string[] order)
    {
        // Arrange
        var document = LocalizationDocumentTestFactory.Read(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.WithReservedOrder(order));

        // Act
        var diagnostics = SchemaLinterTestFactory.Create().Lint(document, "he");

        // Assert
        Assert.Contains(LintingCodes.ReservedEntryOutOfOrder, diagnostics.Codes());
    }
}
