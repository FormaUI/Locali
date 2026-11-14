namespace Forma.Locali.Tests.Rules.Projects;

using Forma.Locali.Projects;
using Forma.Locali.Rules.Projects;

using Microsoft.CodeAnalysis;

public sealed class AmbiguousNameCheckerTests
{
    private static readonly BuiltAssembly Widgets = new("Contoso.Widgets", ["Contoso.Widgets.Bars.Widget"], "Contoso.Widgets");

    [Fact]
    public void Should_report_a_file_both_readings_reach_a_type_by()
    {
        // Arrange — the application declares Scratch.Contoso.Widgets.Bars.Widget, so its own reading of the name reaches a type as Contoso.Widgets' does.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));

        // Act
        var diagnostic = Assert.Single(Check(Application(directory, "namespace Scratch.Contoso.Widgets.Bars; public sealed class Widget;"), file));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.AmbiguousName, diagnostic.Code),
            () => Assert.Equal(file, diagnostic.Path),
            () => Assert.Contains("'Scratch.Contoso.Widgets.Bars'", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains("'Contoso.Widgets'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_report_a_file_in_a_culture_folder_both_readings_reach_a_type_by()
    {
        // Arrange — the culture may stand before the name, which reads the same either way.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "he", "Contoso.Widgets.Bars.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));

        // Act
        var diagnostics = Check(Application(directory, "namespace Scratch.Contoso.Widgets.Bars; public sealed class Widget;"), file);

        // Assert
        Assert.Equal([LintingCodes.AmbiguousName], diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_a_file_whose_readings_reach_different_sections_alone()
    {
        // Arrange — the application's own Gadget section and Contoso.Widgets' Widget section sit in one file, each read for one assembly alone.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.WithSections(("Widget", "Today", "היום"), ("Gadget", "Today", "היום")));

        // Act
        var diagnostics = Check(Application(directory, "namespace Scratch.Contoso.Widgets.Bars; public sealed class Gadget;"), file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_file_only_the_applications_own_reading_reaches_alone()
    {
        // Arrange — the run builds no assembly whose types the file's other reading would reach.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var project = Application(directory, "namespace Scratch.Contoso.Widgets.Bars; public sealed class Widget;");

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new AmbiguousNameChecker(), project, [file]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_file_only_another_assemblys_reading_reaches_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));

        // Act
        var diagnostics = Check(Application(directory, "namespace Scratch.Components; public sealed class Widget;"), file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_librarys_file_alone()
    {
        // Arrange — a library reads its files for its own types alone.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced("namespace Scratch.Contoso.Widgets.Bars; public sealed class Widget;"), false, false);

        // Act
        var diagnostics = Check(project, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    private static LoadedProject Application(TempDirectory directory, string source)
    {
        var compilation = CompilationTestFactory.Unreferenced(source);

        return new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), compilation.WithOptions(compilation.Options.WithOutputKind(OutputKind.ConsoleApplication)), false, false);
    }

    private static IReadOnlyList<LintingDiagnostic> Check(LoadedProject project, params string[] files)
        => ProjectLinterTestFactory.Check(new AmbiguousNameChecker(), project, files, builtAssemblies: [Widgets]);
}
