namespace Forma.Locali.Tests.Rules.Projects;

using Forma.Locali.Projects;
using Forma.Locali.Rules.Projects;

using Microsoft.CodeAnalysis;

public sealed class SecondCultureInNameCheckerTests
{
    private const string Widget = """
        namespace Scratch.Components;

        public sealed class Widget;
        """;

    [Fact]
    public void Should_report_a_file_named_for_another_culture_in_a_culture_folder()
    {
        // Arrange — the runtime reads the file for 'es', and 'he' as the name of a namespace that does not exist.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "es", "he.json"), LocalizationFileFakeData.ForCulture("es", "Today", "Hoy"));

        // Act
        var diagnostic = Assert.Single(Check(directory, Widget, file));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.SecondCultureInName, diagnostic.Code),
            () => Assert.Equal(file, diagnostic.Path),
            () => Assert.Equal(
                "'he' in the name this file is embedded under is a culture other than the file's own 'es', so the runtime reads the file for 'es' with 'he' as part of the name it stands for, which no type reads.",
                diagnostic.Message));
    }

    [Fact]
    public void Should_report_a_type_file_ending_in_another_culture_in_a_culture_folder()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "he", "Components.Widget.es.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostic = Assert.Single(Check(directory, Widget, file));

        // Assert
        Assert.StartsWith("'es' in the name this file is embedded under is a culture other than the file's own 'he'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_report_a_packs_file_named_for_another_culture_in_a_culture_folder()
    {
        // Arrange — the pack's file is read for Contoso.Widgets, and 'he' as a namespace of it that does not exist.
        using var directory = new TempDirectory();
        var target = new LoadedProject(
            Path.Combine(directory.Root, "Widgets", "Contoso.Widgets.csproj"),
            CompilationTestFactory.Building("Contoso.Widgets", "namespace Contoso.Widgets; public sealed class Widget;"),
            false,
            false,
            EmbeddedResourceNames: new Dictionary<string, string>());
        var file = directory.Write(Path.Combine(LocalePackTestFactory.Folder, "es", "Contoso.Widgets", "he.json"), LocalizationFileFakeData.ForCulture("es", "Today", "Hoy"));
        var pack = LocalePackTestFactory.Resolve(directory, "Spanish", target, file);

        // Act
        var diagnostic = Assert.Single(ProjectLinterTestFactory.Check(new SecondCultureInNameChecker(), target, [], packs: [pack]));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Path.GetFullPath(file), diagnostic.Path),
            () => Assert.StartsWith("'he' in the name this file is embedded under is a culture other than the file's own 'es'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(".locales/es/es.json")]
    [InlineData(".locales/es/Components.Widget.es.json")]
    [InlineData(".locales/es/Components.Widget.json")]
    [InlineData("Components/.locales/Widget.es.json")]
    public void Should_leave_a_name_carrying_its_own_culture_alone(string name)
    {
        // Arrange — a part repeating the file's culture is the culture again, which the runtime leaves out of the name.
        using var directory = new TempDirectory();
        var file = directory.Write(name, LocalizationFileFakeData.ForCulture("es", "Today", "Hoy"));

        // Act
        var diagnostics = Check(directory, Widget, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_part_naming_nothing_that_is_no_culture_alone()
    {
        // Arrange — 'Gadgets' names no type, which LOC0112 reports, and is no culture.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "es", "Gadgets.json"), LocalizationFileFakeData.ForCulture("es", "Today", "Hoy"));

        // Act
        var diagnostics = Check(directory, Widget, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_part_naming_a_namespace_alone()
    {
        // Arrange — 'he' is a namespace of the project here, which the runtime reads the file for on purpose.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "es", "he.json"), LocalizationFileFakeData.ForCulture("es", "Today", "Hoy"));

        // Act
        var diagnostics = Check(directory, "namespace Scratch.he; public sealed class Widget;", file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_an_applications_file_for_an_assembly_whose_types_are_out_of_reach_alone()
    {
        // Arrange — the application reads the file for an assembly it references from a package, whose namespaces are unknown.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "es", "Forma.Framework.Localization", "he.json"), LocalizationFileFakeData.ForCulture("es", "Today", "Hoy"));
        var compilation = CompilationTestFactory.Referenced(Widget);
        var project = new LoadedProject(
            Path.Combine(directory.Root, "Scratch.csproj"),
            compilation.WithOptions(compilation.Options.WithOutputKind(OutputKind.ConsoleApplication)),
            false,
            false,
            EmbeddedResourceNames: Names(directory, [file]));

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new SecondCultureInNameChecker(), project, [file]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_read_the_name_a_file_is_embedded_under_rather_than_its_path()
    {
        // Arrange — the project embeds the file under a name of its own, which carries one culture.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "es", "he.json"), LocalizationFileFakeData.ForCulture("es", "Today", "Hoy"));
        var project = Project(directory, Widget, new Dictionary<string, string> { [Path.GetFullPath(file)] = "Scratch.Components.es.json" });

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new SecondCultureInNameChecker(), project, [file]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_file_the_project_does_not_embed_alone()
    {
        // Arrange — the runtime reads no file the project leaves unembedded from its assembly.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "es", "he.json"), LocalizationFileFakeData.ForCulture("es", "Today", "Hoy"));

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new SecondCultureInNameChecker(), Project(directory, Widget, new Dictionary<string, string>()), [file]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_project_whose_items_were_not_read_alone()
    {
        // Arrange — without the project's items, a name of the project's own choosing cannot be told from its path.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "es", "he.json"), LocalizationFileFakeData.ForCulture("es", "Today", "Hoy"));
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced(Widget), false, false);

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new SecondCultureInNameChecker(), project, [file]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_project_that_did_not_compile_alone()
    {
        // Arrange — without the project's types, a part naming a namespace cannot be told from one naming nothing.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "es", "he.json"), LocalizationFileFakeData.ForCulture("es", "Today", "Hoy"));
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), null, false, false, EmbeddedResourceNames: Names(directory, [file]));

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new SecondCultureInNameChecker(), project, [file]);

        // Assert
        Assert.Empty(diagnostics);
    }

    private static IReadOnlyList<LintingDiagnostic> Check(TempDirectory directory, string source, params string[] files)
        => ProjectLinterTestFactory.Check(new SecondCultureInNameChecker(), Project(directory, source, Names(directory, files)), files);

    private static LoadedProject Project(TempDirectory directory, string source, IReadOnlyDictionary<string, string> resourceNames)
        => new(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced(source), false, false, EmbeddedResourceNames: resourceNames);

    // The build embeds each file under the root namespace and its path, every folder joined by a /.
    private static Dictionary<string, string> Names(TempDirectory directory, string[] files)
        => files.ToDictionary(Path.GetFullPath, file => "Scratch/" + Path.GetRelativePath(directory.Root, file).Replace(Path.DirectorySeparatorChar, '/'), StringComparer.OrdinalIgnoreCase);
}
