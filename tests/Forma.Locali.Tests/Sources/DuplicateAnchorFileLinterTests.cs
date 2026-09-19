namespace Forma.Locali.Tests.Sources;

public sealed class DuplicateAnchorFileLinterTests
{
    [Fact]
    public void Should_report_each_file_of_a_pair_standing_for_one_anchor()
    {
        // Arrange — either file may be the stray one, so both are named.
        var first = new PlacedLocaleFile(@"C:\Scratch\Components\Widget\.locales\he.json", "he", ["Scratch.Components.Widget"]);
        var second = new PlacedLocaleFile(@"C:\Scratch\Components\Widget.he.json", "he", ["Scratch.Components.Widget"]);

        // Act
        var diagnostics = new DuplicateAnchorFileLinter().Lint([first, second]);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([LintingCodes.DuplicateAnchorFile, LintingCodes.DuplicateAnchorFile], diagnostics.Codes()),
            () => Assert.Equal([first.Path, second.Path], diagnostics.Select(diagnostic => diagnostic.Path)),
            () => Assert.Contains(second.Path, diagnostics[0].Message, StringComparison.Ordinal),
            () => Assert.Contains(first.Path, diagnostics[1].Message, StringComparison.Ordinal),
            () => Assert.Contains("'Scratch.Components.Widget' in 'he'", diagnostics[0].Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_leave_files_of_one_anchor_in_different_cultures_alone()
    {
        // Arrange
        var first = new PlacedLocaleFile(@"C:\Scratch\Components\.locales\he.json", "he", ["Scratch.Components"]);
        var second = new PlacedLocaleFile(@"C:\Scratch\Components\.locales\es.json", "es", ["Scratch.Components"]);

        // Act
        var diagnostics = new DuplicateAnchorFileLinter().Lint([first, second]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_files_standing_for_different_anchors_alone()
    {
        // Arrange
        var first = new PlacedLocaleFile(@"C:\Scratch\Components\.locales\he.json", "he", ["Scratch.Components"]);
        var second = new PlacedLocaleFile(@"C:\Scratch\Components\Widget\.locales\he.json", "he", ["Scratch.Components.Widget"]);

        // Act
        var diagnostics = new DuplicateAnchorFileLinter().Lint([first, second]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_file_placed_at_two_anchors_alone_when_it_shares_neither()
    {
        // Arrange — one file read at two anchors is one file, not a pair.
        var file = new PlacedLocaleFile(@"C:\Scratch\Components\.locales\he.json", "he", ["Scratch.Components", "Contoso.Components"]);

        // Act
        var diagnostics = new DuplicateAnchorFileLinter().Lint([file]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_name_every_other_file_of_a_trio()
    {
        // Arrange
        var first = new PlacedLocaleFile(@"C:\Scratch\Components\.locales\he.json", "he", ["Scratch.Components"]);
        var second = new PlacedLocaleFile(@"C:\Scratch\Components.he.json", "he", ["Scratch.Components"]);
        var third = new PlacedLocaleFile(@"C:\Scratch\Strings\Components.he.json", "he", ["Scratch.Components"]);

        // Act
        var diagnostics = new DuplicateAnchorFileLinter().Lint([first, second, third]);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(3, diagnostics.Count),
            () => Assert.Contains($"'{second.Path}' and '{third.Path}'", diagnostics[0].Message, StringComparison.Ordinal));
    }
}
