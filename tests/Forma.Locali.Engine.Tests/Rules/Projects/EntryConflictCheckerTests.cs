namespace Forma.Locali.Tests.Rules.Projects;

using Forma.Locali.Projects;
using Forma.Locali.Rules.Projects;

using Microsoft.CodeAnalysis;

public sealed class EntryConflictCheckerTests
{
    private const string Widget = """
        namespace Scratch.Components;

        public sealed class Widget;
        """;

    [Fact]
    public void Should_report_an_entry_two_files_give_different_texts_at_the_file_whose_name_sorts_first()
    {
        // Arrange — a culture folder and a folder's own .locales both stand for Components, and the name sorting last wins.
        using var directory = new TempDirectory();
        var setAside = directory.Write(Path.Combine(".locales", "he", "Components.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var kept = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "עכשיו"));

        // Act
        var diagnostic = Assert.Single(Check(directory, Widget, setAside, kept));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.EntryConflict, diagnostic.Code),
            () => Assert.Equal(setAside, diagnostic.Path),
            () => Assert.Contains("'Today' different texts for 'Scratch.Components.Widget' in 'he'", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains(Path.GetRelativePath(Path.GetDirectoryName(setAside)!, kept), diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_leave_two_files_giving_an_entry_the_same_text_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var first = directory.Write(Path.Combine(".locales", "he", "Components.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var second = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));

        // Act
        var diagnostics = Check(directory, Widget, first, second);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_shared_entry_two_files_give_different_texts()
    {
        // Arrange
        using var directory = new TempDirectory();
        var setAside = directory.Write(Path.Combine(".locales", "he", "Components.json"), LocalizationFileFakeData.WithSharedSection("Widget", "Today", "היום"));
        var kept = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.WithSharedSection("Widget", "Today", "עכשיו"));

        // Act
        var diagnostic = Assert.Single(Check(directory, Widget, setAside, kept));

        // Assert
        Assert.Contains("the shared entry 'Today'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_leave_an_entry_no_type_reads_alone()
    {
        // Arrange — the section names no type, which LOC0112 reports, so its texts change nothing a reader sees.
        using var directory = new TempDirectory();
        var first = directory.Write(Path.Combine(".locales", "he", "Components.json"), LocalizationFileFakeData.WithSection("Gadget", "Today", "היום"));
        var second = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.WithSection("Gadget", "Today", "עכשיו"));

        // Act
        var diagnostics = Check(directory, Widget, first, second);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_conflict_a_type_outside_any_namespace_meets()
    {
        // Arrange — a type outside any namespace belongs to the root, and its section sits at the root's files.
        using var directory = new TempDirectory();
        var first = directory.Write(Path.Combine(".locales", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var second = directory.Write("he.json", LocalizationFileFakeData.WithSection("Widget", "Today", "עכשיו"));
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced("public sealed class Widget;"), false, false);

        // Act
        var diagnostic = Assert.Single(ProjectLinterTestFactory.Check(new EntryConflictChecker(), project, [first, second]));

        // Assert
        Assert.Equal(first, diagnostic.Path);
    }

    [Fact]
    public void Should_leave_a_conflict_no_types_lookup_meets_alone()
    {
        // Arrange — the files are named under Contoso, outside the root namespace Contoso.Widgets, so they are read by full names a type under the root never looks up.
        using var directory = new TempDirectory();
        var first = directory.Write(Path.Combine(".locales", "he.json"), LocalizationFileFakeData.WithSection("Widgets.Bar", "Today", "היום"));
        var second = directory.Write("he.json", LocalizationFileFakeData.WithSection("Widgets.Bar", "Today", "עכשיו"));
        var project = new LoadedProject(Path.Combine(directory.Root, "Contoso.Widgets.csproj"), CompilationTestFactory.Building("Contoso.Widgets", "namespace Contoso.Widgets; public sealed class Bar;"), false, false, "Contoso");

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new EntryConflictChecker(), project, [first, second]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_an_entry_two_of_an_applications_files_give_another_assembly_of_the_run_different_texts()
    {
        // Arrange — the application reads both files for Contoso.Widgets, a culture folder before the assembly's name and one after it.
        using var directory = new TempDirectory();
        var first = directory.Write(Path.Combine(".locales", "Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var second = directory.Write(Path.Combine(".locales", "he", "Contoso.Widgets", "Bars.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "עכשיו"));
        var compilation = CompilationTestFactory.Unreferenced(Widget);
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), compilation.WithOptions(compilation.Options.WithOutputKind(OutputKind.ConsoleApplication)), false, false);
        var widgets = new BuiltAssembly("Contoso.Widgets", ["Contoso.Widgets.Bars.Widget"], "Contoso.Widgets");

        // Act
        var diagnostic = Assert.Single(ProjectLinterTestFactory.Check(new EntryConflictChecker(), project, [first, second], builtAssemblies: [widgets]));

        // Assert
        Assert.Contains("for 'Contoso.Widgets.Bars.Widget'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_leave_an_embedded_file_and_a_packs_giving_an_entry_different_texts_alone()
    {
        // Arrange — the pack is a later source than the assembly, so its text wins without a conflict.
        using var directory = new TempDirectory();
        var target = new LoadedProject(Path.Combine(directory.Root, "Widgets", "Contoso.Widgets.csproj"), CompilationTestFactory.Building("Contoso.Widgets", "namespace Contoso.Widgets.Bars; public sealed class Widget;"), false, false, EmbeddedResourceNames: new Dictionary<string, string>());
        var own = directory.Write(Path.Combine("Widgets", "Bars", ".locales", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var packFile = directory.Write(Path.Combine(LocalePackTestFactory.Folder, "Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "עכשיו"));
        var pack = LocalePackTestFactory.Resolve(directory, target, packFile);
        var project = target with { EmbeddedResourceNames = new Dictionary<string, string> { [Path.GetFullPath(own)] = "Contoso.Widgets.Bars..locales.he.json" } };

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new EntryConflictChecker(), project, [own], packs: [pack]);

        // Assert
        Assert.Empty(diagnostics);
    }

    private static IReadOnlyList<LintingDiagnostic> Check(TempDirectory directory, string source, params string[] files)
    {
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced(source), false, false);

        return ProjectLinterTestFactory.Check(new EntryConflictChecker(), project, files);
    }
}
