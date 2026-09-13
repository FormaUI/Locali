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
        var file = directory.Write(Path.Combine("Components", "Gallery.he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostic = Assert.Single(Lint(directory, Widget, file));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintCodes.UnreachableTranslation, diagnostic.Code),
            () => Assert.Equal(file, diagnostic.Path),
            () => Assert.Contains("Scratch.Components.Gallery", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_leave_the_file_named_for_a_type_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", "Widget.he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Lint(directory, Widget, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_let_the_file_at_the_conventional_position_answer_before_one_shedding_folders()
    {
        // Arrange — a deeper file sheds to the same name, and is listed first.
        using var directory = new TempDirectory();
        var shed = directory.Write(Path.Combine("Resources", "Components", "Widget.he.json"), LocalizationFileFakeData.Valid);
        var conventional = directory.Write(Path.Combine("Components", "Widget.he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Lint(directory, Widget, shed, conventional);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Path == conventional);
    }

    [Fact]
    public void Should_leave_a_folders_file_alone_when_its_sections_name_types()
    {
        // Arrange — the file named for the folder, inside it, with a section per type.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", "Components.he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));

        // Act
        var diagnostics = Lint(directory, Widget, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_folders_file_alone_beside_the_folder_too()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write("Components.he.json", LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));

        // Act
        var diagnostics = Lint(directory, Widget, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_section_that_names_no_type()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", "Components.he.json"), LocalizationFileFakeData.WithSections(("Widget", "Today", "היום"), ("Gallery", "Today", "היום")));

        // Act
        var diagnostic = Assert.Single(Lint(directory, Widget, file));

        // Assert
        Assert.Contains("Section 'Gallery'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_leave_a_file_that_ignores_the_rule_alone()
    {
        // Arrange — a section names no type, which the file says is meant.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", "Components.he.json"), LocalizationFileFakeData.WithIgnore(["LOC0112"], ("Widget", "Today", "היום"), ("Gallery", "Today", "היום")));

        // Act
        var diagnostics = Lint(directory, Widget, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_entries_outside_any_section_in_a_folders_file()
    {
        // Arrange — a folder's file is read for its sections; a stray entry at the top level belongs to no type.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", "Components.he.json"), LocalizationFileFakeData.WithSectionAndStray("Widget", "Today", "היום", "Stray", "תועה"));

        // Act
        var diagnostic = Assert.Single(Lint(directory, Widget, file));

        // Assert
        Assert.Contains("outside any section", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_reach_a_type_through_the_assemblys_file()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write("Scratch.he.json", LocalizationFileFakeData.WithSection("Components.Widget", "Today", "היום"));

        // Act
        var diagnostics = Lint(directory, Widget, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_reach_a_generic_type_whose_arity_the_file_drops()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write("GenericArity.he.json", LocalizationFileFakeData.Valid);

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
        var file = directory.Write("GlobalRoot.he.json", LocalizationFileFakeData.Valid);

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
        var file = directory.Write(Path.Combine("Emojis", "TwemojiEmojiProvider.he.json"), LocalizationFileFakeData.Valid);
        var compilation = CompilationTestFactory.Unreferenced("namespace Forma.Framework.Emojis; public sealed class TwemojiEmojiProvider;");
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), compilation, false, false, "Forma.Framework");

        // Act
        var diagnostics = new ReachabilityLinter().Lint(TranslationFiles.Resolve(project, [file])!);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_stay_silent_on_a_name_two_files_claim()
    {
        // Arrange — one of the two is unreachable, but which depends on a ResourcesPath the linter cannot see.
        using var directory = new TempDirectory();
        var scoped = directory.Write(Path.Combine("Resources", "Components", "Widget.he.json"), LocalizationFileFakeData.Valid);
        var plain = directory.Write(Path.Combine("Components", "Widget.he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Lint(directory, Widget, scoped, plain);

        // Assert
        Assert.Empty(diagnostics);
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

    private static IReadOnlyList<LintDiagnostic> Lint(TempDirectory directory, string source, params string[] files)
    {
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced(source), false, false);

        return new ReachabilityLinter().Lint(TranslationFiles.Resolve(project, files)!);
    }
}
