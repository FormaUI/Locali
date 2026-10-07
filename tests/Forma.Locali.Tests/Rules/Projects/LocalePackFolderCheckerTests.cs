namespace Forma.Locali.Tests.Rules.Projects;

using Forma.Locali.Projects;
using Forma.Locali.Rules.Projects;

using Microsoft.CodeAnalysis;

public sealed class LocalePackFolderCheckerTests
{
    private const string Widget = "namespace Contoso.Widgets; public sealed class Widget;";

    [Fact]
    public void Should_report_a_folder_naming_no_assembly_of_the_solution()
    {
        // Arrange — a misspelled folder loads with the pack and answers no lookup.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgetz", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var pack = Pack(directory, file);
        var target = Project(CompilationTestFactory.Building("Contoso.Widgets", Widget));

        // Act
        var diagnostic = Assert.Single(Check(pack, [file], new LoadedProjects([pack, target], IsWholeSolution: true)));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.UnknownLocalePackAssembly, diagnostic.Code),
            () => Assert.Equal(pack.Path, diagnostic.Path),
            () => Assert.Contains("'Contoso.Widgetz'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_leave_a_folder_naming_a_project_of_the_solution_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var pack = Pack(directory, file);
        var target = Project(CompilationTestFactory.Building("Contoso.Widgets", Widget));

        // Act
        var diagnostics = Check(pack, [file], new LoadedProjects([pack, target], IsWholeSolution: true));

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_folder_naming_an_assembly_a_project_references_alone()
    {
        // Arrange — the assembly arrives from a package, so the folder names something real whose types are out of reach.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var pack = Pack(directory, file);
        var host = Project(CompilationTestFactory.Building("Contoso.Host").AddReferences(CompilationTestFactory.Building("Contoso.Widgets", Widget).ToMetadataReference()));

        // Act
        var diagnostics = Check(pack, [file], new LoadedProjects([pack, host], IsWholeSolution: true));

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_stay_silent_when_the_run_named_no_solution()
    {
        // Arrange — a directory lists no project but what sits beneath it, so the assembly may be built elsewhere.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgetz", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var pack = Pack(directory, file);

        // Act
        var diagnostics = Check(pack, [file], new LoadedProjects([pack], IsWholeSolution: false));

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_stay_silent_when_a_project_of_the_solution_did_not_compile()
    {
        // Arrange — the project that did not compile may be the one building the assembly.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgetz", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var pack = Pack(directory, file);
        var broken = Project(null);

        // Act
        var diagnostics = Check(pack, [file], new LoadedProjects([pack, broken], IsWholeSolution: true));

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_project_that_is_not_a_pack_alone()
    {
        // Arrange — an ordinary project's folders are namespaces, not assemblies.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgetz", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced(Widget), false, false);

        // Act
        var diagnostics = Check(project, [file], new LoadedProjects([project], IsWholeSolution: true));

        // Assert
        Assert.Empty(diagnostics);
    }

    private static IReadOnlyList<LintingDiagnostic> Check(LoadedProject project, IReadOnlyList<string> files, LoadedProjects projects)
        => ProjectLinterTestFactory.Check(new LocalePackFolderChecker(), project, files, packs: LocalePackFiles.ResolveAll(LocaleFormatTestFactory.Json.RuntimeFormat, projects, _ => files));

    private static LoadedProject Pack(TempDirectory directory, params string[] files)
    {
        var resourceNames = files.ToDictionary(
            Path.GetFullPath,
            file => Path.GetRelativePath(directory.Root, file).Replace(Path.DirectorySeparatorChar, '.').Replace(Path.AltDirectorySeparatorChar, '.'),
            StringComparer.OrdinalIgnoreCase);

        return new LoadedProject(Path.Combine(directory.Root, "Contoso.Widgets.Locales.Hebrew.csproj"), CompilationTestFactory.Building("Contoso.Widgets.Locales.Hebrew"), false, false, EmbeddedResourceNames: resourceNames, IsLocalePack: true);
    }

    private static LoadedProject Project(Compilation? compilation)
        => new(Path.Combine(Path.GetTempPath(), "Target", "Target.csproj"), compilation, false, false);
}
