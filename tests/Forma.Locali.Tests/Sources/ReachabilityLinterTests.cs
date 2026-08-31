namespace Forma.Locali.Tests.Sources;

using System.Globalization;

using Microsoft.CodeAnalysis;

public sealed class ReachabilityLinterTests
{
    private const string ExpectedName = "Scratch.Components.Explorer.FormaGallery";

    [Fact]
    public void Should_report_a_file_no_type_answers_to()
    {
        // Arrange
        var file = Unresolved(Path.Combine("C:", "Scratch", "FormaGallery.he.json"));

        // Act
        var diagnostics = new ReachabilityLinter().Lint([file]);

        // Assert
        var diagnostic = Assert.Single(diagnostics);

        Assert.Multiple(
            () => Assert.Equal(LintCodes.UnreachableTranslation, diagnostic.Code),
            () => Assert.Equal(file.Path, diagnostic.Path),
            () => Assert.Contains(ExpectedName, diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_leave_a_file_a_type_still_answers_to_alone()
    {
        // Arrange
        var compilation = CompilationTestFactory.Unreferenced("""
            namespace Scratch;

            public sealed class FormaExplorer;
            """);
        var file = new TranslationFile(
            Path.Combine("C:", "Scratch", "FormaExplorer.he.json"),
            CultureInfo.GetCultureInfo("he"),
            compilation.GetTypeByMetadataName("Scratch.FormaExplorer"),
            "Scratch.FormaExplorer");

        // Act
        var diagnostics = new ReachabilityLinter().Lint([file]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_every_file_no_type_answers_to()
    {
        // Arrange
        var first = Unresolved(Path.Combine("C:", "Scratch", "FormaGallery.he.json"));
        var second = Unresolved(Path.Combine("C:", "Scratch", "FormaGallery.es.json"));

        // Act
        var diagnostics = new ReachabilityLinter().Lint([first, second]);

        // Assert
        Assert.Equal(2, diagnostics.Count);
    }

    [Fact]
    public void Should_report_nothing_when_the_project_owns_no_translation_files()
    {
        // Act
        var diagnostics = new ReachabilityLinter().Lint([]);

        // Assert
        Assert.Empty(diagnostics);
    }

    private static TranslationFile Unresolved(string path)
        => new(path, CultureInfo.GetCultureInfo("he"), null, ExpectedName);
}
