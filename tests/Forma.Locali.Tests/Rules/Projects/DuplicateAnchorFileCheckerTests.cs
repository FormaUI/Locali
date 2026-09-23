namespace Forma.Locali.Tests.Rules.Projects;

using Forma.Locali.Projects;
using Forma.Locali.Rules.Projects;

public sealed class DuplicateAnchorFileCheckerTests
{
    [Fact]
    public void Should_report_each_file_of_a_pair_standing_for_one_anchor()
    {
        // Arrange — either file may be the stray one, so both are named.
        var first = new PlacedLocaleFile(@"C:\Scratch\Components\Widget\.locales\he.json", "he", ["Scratch.Components.Widget"]);
        var second = new PlacedLocaleFile(@"C:\Scratch\Components\Widget.he.json", "he", ["Scratch.Components.Widget"]);

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new DuplicateAnchorFileChecker(), [first, second]);

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
        var diagnostics = ProjectLinterTestFactory.Check(new DuplicateAnchorFileChecker(), [first, second]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_pack_file_written_in_both_forms()
    {
        // Arrange — a pack is read by the host folder's paths, so a folder's file directly in the folder and in the folder's own .locales stand for one anchor.
        using var directory = new TempDirectory();
        var target = new LoadedProject(Path.Combine(directory.Root, "Widgets", "Contoso.Widgets.csproj"), CompilationTestFactory.Building("Contoso.Widgets", "namespace Contoso.Widgets.Bars; public sealed class Widget;"), false, false, EmbeddedResourceNames: new Dictionary<string, string>());
        var flat = directory.Write(Path.Combine(LocalePackTestFactory.Folder, "Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var nested = directory.Write(Path.Combine(LocalePackTestFactory.Folder, "Contoso.Widgets", "Bars", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var pack = LocalePackTestFactory.Resolve(directory, target, flat, nested);

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new DuplicateAnchorFileChecker(), target, [], packs: [pack]);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([LintingCodes.DuplicateAnchorFile, LintingCodes.DuplicateAnchorFile], diagnostics.Codes()),
            () => Assert.Equal([flat, nested], diagnostics.Select(diagnostic => diagnostic.Path)),
            () => Assert.Contains("'Contoso.Widgets.Bars' in 'he'", diagnostics[0].Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_leave_files_standing_for_different_anchors_alone()
    {
        // Arrange
        var first = new PlacedLocaleFile(@"C:\Scratch\Components\.locales\he.json", "he", ["Scratch.Components"]);
        var second = new PlacedLocaleFile(@"C:\Scratch\Components\Widget\.locales\he.json", "he", ["Scratch.Components.Widget"]);

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new DuplicateAnchorFileChecker(), [first, second]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_file_placed_at_two_anchors_alone_when_it_shares_neither()
    {
        // Arrange — one file read at two anchors is one file, not a pair.
        var file = new PlacedLocaleFile(@"C:\Scratch\Components\.locales\he.json", "he", ["Scratch.Components", "Contoso.Components"]);

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new DuplicateAnchorFileChecker(), [file]);

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
        var diagnostics = ProjectLinterTestFactory.Check(new DuplicateAnchorFileChecker(), [first, second, third]);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(3, diagnostics.Count),
            () => Assert.Contains($"'{second.Path}' and '{third.Path}'", diagnostics[0].Message, StringComparison.Ordinal));
    }
}
