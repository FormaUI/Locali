namespace Forma.Locali.Tests.Rules.Projects;

using Forma.Locali.Projects;
using Forma.Locali.Rules.Projects;

using Microsoft.CodeAnalysis;

public sealed class UnreachableLocalizationCheckerTests
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
            () => Assert.Equal(LintingCodes.UnreachableLocalization, diagnostic.Code),
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
    public void Should_leave_a_rooted_section_naming_a_type_alone()
    {
        // Arrange — the file sits under a folder no type is named for, and its section names the type from the root.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Strings", ".locales", "he.json"), LocalizationFileFakeData.WithSection("@.Components.Widget", "Today", "היום"));

        // Act
        var diagnostics = Lint(directory, Widget, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_rooted_section_naming_no_type()
    {
        // Arrange — the folder's file reaches its type through one section, and names no type through the rooted one.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", ".locales", "he.json"), """{ "$localization": "SchemaVersion: 1.0.0; Culture: he;", "Widget": { "Today": "היום" }, "@.Components.Gadget": { "Today": "היום" } }""");

        // Act
        var diagnostic = Assert.Single(Lint(directory, Widget, file));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.UnreachableLocalization, diagnostic.Code),
            () => Assert.Contains("'@.Components.Gadget'", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains("from the assembly's root", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_read_a_file_named_for_its_type_outside_any_folder_of_locale_files()
    {
        // Arrange — the default reading takes a file wherever it sits, its folders and its name alike.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", "Widget.he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Lint(directory, Widget, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_file_no_path_the_assemblys_config_names_reads()
    {
        // Arrange — the paths a config names replace the default reading.
        using var directory = new TempDirectory();
        var config = directory.Write(".locale.json", """{ "paths": ["$RootDir/strings-$Culture/$AnchorName$Extension"] }""");
        var file = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var resourceNames = new Dictionary<string, string>
        {
            [Path.GetFullPath(config)] = "Scratch/.locale.json",
            [Path.GetFullPath(file)] = "Scratch/Components/.locales/he.json",
        };

        // Act
        var diagnostic = Assert.Single(Lint(directory, Widget, resourceNames, file));

        // Assert
        Assert.Contains("no locale path", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_read_a_file_by_the_paths_the_assemblys_config_names()
    {
        // Arrange — the config file the project embeds puts a form in force that the defaults leave out.
        using var directory = new TempDirectory();
        var config = directory.Write(".locale.json", """{ "paths": ["$RootDir/$AnchorName.$Culture$Extension"] }""");
        var file = directory.Write(Path.Combine("Components", "Widget.he.json"), LocalizationFileFakeData.Valid);
        var resourceNames = new Dictionary<string, string>
        {
            [Path.GetFullPath(config)] = "Scratch/.locale.json",
            [Path.GetFullPath(file)] = "Scratch/Components/Widget.he.json",
        };

        // Act
        var diagnostics = Lint(directory, Widget, resourceNames, file);

        // Assert
        Assert.Empty(diagnostics);
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
        var diagnostics = ProjectLinterTestFactory.Check(new UnreachableLocalizationChecker(), project, [file]);

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
            () => Assert.Equal(LintingCodes.UnreachableLocalization, diagnostic.Code),
            () => Assert.Equal(scoped, diagnostic.Path));
    }

    [Fact]
    public void Should_report_a_pack_section_naming_no_type()
    {
        // Arrange — a pack's file is read for the assembly its name starts with, so its sections are held to that assembly's types.
        using var directory = new TempDirectory();
        var target = new LoadedProject(Path.Combine(directory.Root, "Widgets", "Contoso.Widgets.csproj"), CompilationTestFactory.Building("Contoso.Widgets", "namespace Contoso.Widgets.Bars; public sealed class Widget;"), false, false, EmbeddedResourceNames: new Dictionary<string, string>());
        var packFile = directory.Write(Path.Combine(LocalePackTestFactory.Folder, "Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.WithSections(("Widget", "Today", "היום"), ("Gadget", "Today", "היום")));
        var pack = LocalePackTestFactory.Resolve(directory, target, packFile);

        // Act
        var diagnostic = Assert.Single(ProjectLinterTestFactory.Check(new UnreachableLocalizationChecker(), target, [], packs: [pack]));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.UnreachableLocalization, diagnostic.Code),
            () => Assert.Equal(packFile, diagnostic.Path),
            () => Assert.Contains("'Gadget'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_leave_a_pack_file_naming_a_longer_assembly_to_that_assembly()
    {
        // Arrange — the runtime reads the file for both assemblies its name starts with, the shorter as another assembly's namespace.
        using var directory = new TempDirectory();
        var target = new LoadedProject(Path.Combine(directory.Root, "Widgets", "Contoso.Widgets.csproj"), CompilationTestFactory.Building("Contoso.Widgets", "namespace Contoso.Widgets; public sealed class Widget;"), false, false, EmbeddedResourceNames: new Dictionary<string, string>());
        var bars = new LoadedProject(Path.Combine(directory.Root, "Bars", "Contoso.Widgets.Bars.csproj"), CompilationTestFactory.Building("Contoso.Widgets.Bars", "namespace Contoso.Widgets.Bars; public sealed class Gadget;"), false, false);
        var packFile = directory.Write(Path.Combine(LocalePackTestFactory.Folder, "Contoso.Widgets.Bars", "he.json"), LocalizationFileFakeData.WithSection("Gadget", "Today", "היום"));
        var pack = LocalePackTestFactory.Resolve(directory, [target, bars], packFile);

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new UnreachableLocalizationChecker(), target, [], packs: [pack]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_pack_file_a_type_reads_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var target = new LoadedProject(Path.Combine(directory.Root, "Widgets", "Contoso.Widgets.csproj"), CompilationTestFactory.Building("Contoso.Widgets", "namespace Contoso.Widgets.Bars; public sealed class Widget;"), false, false, EmbeddedResourceNames: new Dictionary<string, string>());
        var packFile = directory.Write(Path.Combine(LocalePackTestFactory.Folder, "Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var pack = LocalePackTestFactory.Resolve(directory, target, packFile);

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new UnreachableLocalizationChecker(), target, [], packs: [pack]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_section_an_applications_file_gives_an_assembly_of_the_run_that_names_none_of_its_types()
    {
        // Arrange — the application reads its file for Contoso.Widgets, whose types the run knows, so the file is held to them.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.WithSections(("Widget", "Today", "היום"), ("Gadget", "Today", "היום")));
        var widgets = new BuiltAssembly("Contoso.Widgets", ["Contoso.Widgets.Bars.Widget"], "Contoso.Widgets");

        // Act
        var diagnostic = Assert.Single(ProjectLinterTestFactory.Check(new UnreachableLocalizationChecker(), Application(directory), [file], builtAssemblies: [widgets]));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(file, diagnostic.Path),
            () => Assert.Contains("'Gadget'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_leave_an_applications_file_a_type_of_another_assembly_of_the_run_reads_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "he", "Contoso.Widgets", "Bars.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var widgets = new BuiltAssembly("Contoso.Widgets", ["Contoso.Widgets.Bars.Widget"], "Contoso.Widgets");

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new UnreachableLocalizationChecker(), Application(directory), [file], builtAssemblies: [widgets]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_an_applications_file_laid_out_under_a_namespace_of_its_own_that_no_type_reads()
    {
        // Arrange — the name starts a namespace of the application's own, so no assembly the run does not know could start it.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", "Gallery", ".locales", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostic = Assert.Single(ProjectLinterTestFactory.Check(new UnreachableLocalizationChecker(), Application(directory), [file]));

        // Assert
        Assert.Contains("'Scratch.Components.Gallery'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_leave_an_applications_file_whose_name_goes_on_past_its_own_into_no_namespace_of_its_own_alone()
    {
        // Arrange — Scratch.Plugin may be an assembly the application loads at run time, which the run does not know.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "Scratch.Plugin", "Bars", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new UnreachableLocalizationChecker(), Application(directory), [file]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_librarys_file_named_for_an_assembly_no_type_of_its_own_reads()
    {
        // Arrange — only an application or a pack reads a file by an assembly's name, so a library's such file is read for its own types alone.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "Forma.Framework.Localization", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Referenced(Widget), false, false);

        // Act
        var diagnostic = Assert.Single(ProjectLinterTestFactory.Check(new UnreachableLocalizationChecker(), project, [file]));

        // Assert
        Assert.Contains("the assembly name 'Forma.Framework.Localization'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_leave_an_applications_file_for_an_assembly_no_project_of_the_run_builds_alone()
    {
        // Arrange — the application reads the file for an assembly it references from a package, whose types are out of reach.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "Forma.Framework.Localization", "Components", "he.json"), LocalizationFileFakeData.Valid);
        var compilation = CompilationTestFactory.Referenced(Widget);
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), compilation.WithOptions(compilation.Options.WithOutputKind(OutputKind.ConsoleApplication)), false, false);

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new UnreachableLocalizationChecker(), project, [file]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_nothing_when_the_project_owns_no_locale_files()
    {
        // Arrange
        using var directory = new TempDirectory();

        // Act
        var diagnostics = Lint(directory, Widget);

        // Assert
        Assert.Empty(diagnostics);
    }

    private static IReadOnlyList<LintingDiagnostic> Lint(TempDirectory directory, string source, params string[] files)
        => Lint(directory, source, null, files);

    private static LoadedProject Application(TempDirectory directory)
    {
        var compilation = CompilationTestFactory.Unreferenced(Widget);

        return new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), compilation.WithOptions(compilation.Options.WithOutputKind(OutputKind.ConsoleApplication)), false, false);
    }

    private static IReadOnlyList<LintingDiagnostic> Lint(TempDirectory directory, string source, IReadOnlyDictionary<string, string>? resourceNames, params string[] files)
    {
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced(source), false, false, EmbeddedResourceNames: resourceNames);

        return ProjectLinterTestFactory.Check(new UnreachableLocalizationChecker(), project, files);
    }
}
