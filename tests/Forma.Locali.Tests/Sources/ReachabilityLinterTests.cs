namespace Forma.Locali.Tests.Sources;

using Forma.Locali.Projects;

public sealed class ReachabilityLinterTests
{
    private const string Widget = """
        namespace Scratch.Components;

        public sealed class Widget;
        """;

    [Fact]
    public void Should_report_a_file_no_type_reads()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", "Gallery", ".locales", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostic = Assert.Single(Lint(directory, Widget, file));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.UnreachableTranslation, diagnostic.Code),
            () => Assert.Equal(file, diagnostic.Path),
            () => Assert.Contains("'Scratch.Components.Gallery'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_leave_the_file_in_a_types_own_folder_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", "Widget", ".locales", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Lint(directory, Widget, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_folders_file_alone_when_its_sections_name_types()
    {
        // Arrange — the folder's own file, with a section per type.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));

        // Act
        var diagnostics = Lint(directory, Widget, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_file_in_a_form_no_path_reads()
    {
        // Arrange — a file beside its type is no default form, so nothing places it.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", "Widget.he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostic = Assert.Single(Lint(directory, Widget, file));

        // Assert
        Assert.Contains("no locale path", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_report_a_section_that_names_no_type()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.WithSections(("Widget", "Today", "היום"), ("Gallery", "Today", "היום")));

        // Act
        var diagnostic = Assert.Single(Lint(directory, Widget, file));

        // Assert
        Assert.Contains("Section 'Gallery'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_report_even_a_file_that_ignores_the_rule()
    {
        // Arrange — a section names no type, which the file says is meant; leaving it out is a caller's decision.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.WithIgnore(["LOC0112"], ("Widget", "Today", "היום"), ("Gallery", "Today", "היום")));

        // Act
        var diagnostics = Lint(directory, Widget, file);

        // Assert
        Assert.Equal([LintingCodes.UnreachableTranslation], diagnostics.Codes());
    }

    [Fact]
    public void Should_report_entries_outside_any_section_in_a_folders_file()
    {
        // Arrange — a folder's file is read for its sections; a stray entry at the top level belongs to no type.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.WithSectionAndStray("Widget", "Today", "היום", "Stray", "תועה"));

        // Act
        var diagnostic = Assert.Single(Lint(directory, Widget, file));

        // Assert
        Assert.Multiple(
            () => Assert.Contains("'Scratch.Components'", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains("outside any section", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_reach_a_type_through_the_roots_file()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "he.json"), LocalizationFileFakeData.WithSection("Components.Widget", "Today", "היום"));

        // Act
        var diagnostics = Lint(directory, Widget, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_reach_a_generic_type_whose_arity_the_folder_drops()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("GenericArity", ".locales", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Lint(directory, "namespace Scratch; public sealed class GenericArity<TValue>;", file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_reach_a_type_in_the_global_namespace()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("GlobalRoot", ".locales", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Lint(directory, "public sealed class GlobalRoot;", file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_reach_a_type_under_a_root_namespace_apart_from_the_assembly_name()
    {
        // Arrange — an AddOn embeds under the namespace it borrows, not its own name.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Emojis", "TwemojiEmojiProvider", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var compilation = CompilationTestFactory.Unreferenced("namespace Forma.Framework.Emojis; public sealed class TwemojiEmojiProvider;");
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), compilation, false, false, "Forma.Framework");

        // Act
        var diagnostics = new ReachabilityLinter().Lint(TranslationFiles.Resolve(project, [file])!);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_file_under_a_folder_the_types_name_does_not_spell()
    {
        // Arrange — the type is Scratch.Components.Widget, so a leading Resources folder places the file where no type reads.
        using var directory = new TempDirectory();
        var scoped = directory.Write(Path.Combine("Resources", "Components", "Widget", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var plain = directory.Write(Path.Combine("Components", "Widget", ".locales", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostic = Assert.Single(Lint(directory, Widget, scoped, plain));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.UnreachableTranslation, diagnostic.Code),
            () => Assert.Equal(scoped, diagnostic.Path));
    }

    [Fact]
    public void Should_report_nothing_when_the_project_owns_no_translation_files()
    {
        // Arrange
        using var directory = new TempDirectory();

        // Act
        var diagnostics = Lint(directory, Widget);

        // Assert
        Assert.Empty(diagnostics);
    }

    private static IReadOnlyList<LintingDiagnostic> Lint(TempDirectory directory, string source, params string[] files)
    {
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced(source), false, false);

        return new ReachabilityLinter().Lint(TranslationFiles.Resolve(project, files)!);
    }
}
