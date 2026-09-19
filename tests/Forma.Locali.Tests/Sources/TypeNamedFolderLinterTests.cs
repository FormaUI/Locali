namespace Forma.Locali.Tests.Sources;

using Forma.Locali.Projects;

public sealed class TypeNamedFolderLinterTests
{
    private const string Widget = """
        namespace Scratch.Components;

        public sealed class Widget;
        """;

    [Fact]
    public void Should_report_a_folder_named_for_a_type()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", "Widget", ".locales", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostic = Assert.Single(Lint(directory, Widget, file));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.TypeNamedFolder, diagnostic.Code),
            () => Assert.Equal(file, diagnostic.Path),
            () => Assert.Contains("'Scratch.Components.Widget'", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains("'Widget.he.json'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_leave_a_namespaces_folder_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));

        // Act
        var diagnostics = Lint(directory, Widget, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_types_own_file_alone()
    {
        // Arrange — the type's file is named for it, in its namespace folder's locale files.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", ".locales", "Widget.he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Lint(directory, Widget, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    private static IReadOnlyList<LintingDiagnostic> Lint(TempDirectory directory, string source, params string[] files)
    {
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced(source), false, false);

        return new TypeNamedFolderLinter().Lint(ProjectLocaleFiles.Resolve(project, files)!);
    }
}
