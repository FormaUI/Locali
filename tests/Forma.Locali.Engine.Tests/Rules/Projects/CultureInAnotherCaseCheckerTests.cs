namespace Forma.Locali.Tests.Rules.Projects;

using Forma.Locali.Projects;
using Forma.Locali.Rules.Projects;

public sealed class CultureInAnotherCaseCheckerTests
{
    private const string Widget = """
        namespace Scratch.Components;

        public sealed class Widget;
        """;

    [Fact]
    public void Should_report_a_culture_folder_written_in_another_case()
    {
        // Arrange — 'ES' is no culture to the runtime, so the file has none and is never read.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "ES", "Components.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostic = Assert.Single(Check(directory, Widget, file));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.CultureInAnotherCase, diagnostic.Code),
            () => Assert.Equal(file, diagnostic.Path),
            () => Assert.Contains("'ES' in the name this file is embedded under is the culture 'es'", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains("finds no culture", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_report_a_culture_whose_region_is_written_in_another_case()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "en-us", "Components.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostic = Assert.Single(Check(directory, Widget, file));

        // Assert
        Assert.Contains("the culture 'en-US'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_report_a_culture_written_in_another_case_beside_one_written_rightly()
    {
        // Arrange — the file is Hebrew, and 'Es' is read as part of the name it stands for.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", ".locales", "Es.he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostic = Assert.Single(Check(directory, Widget, file));

        // Assert
        Assert.Contains("as part of the name", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_leave_a_part_naming_a_type_alone()
    {
        // Arrange — 'Id' is the Indonesian culture in another case, and a type of the project here.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", ".locales", "Id.he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Check(directory, "namespace Scratch.Components; public sealed class Id;", file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_part_naming_a_namespace_alone()
    {
        // Arrange — 'Is' is the Icelandic culture in another case, and a namespace of the project here.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Is", ".locales", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Check(directory, "namespace Scratch.Is; public sealed class Widget;", file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_read_the_name_a_file_is_embedded_under_rather_than_its_path()
    {
        // Arrange — the project embeds the file under a name of its own, which carries the culture as .NET names it.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "ES", "Components.json"), LocalizationFileFakeData.ForCulture("es", "Today", "Hoy"));
        var project = Project(directory, Widget, new Dictionary<string, string> { [Path.GetFullPath(file)] = "Scratch.Components.es.json" });

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new CultureInAnotherCaseChecker(), project, [file]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("he.json")]
    [InlineData("Widget.es-419.json")]
    [InlineData("Toolbar.he.json")]
    public void Should_leave_a_name_with_no_culture_in_another_case_alone(string fileName)
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", ".locales", fileName), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Check(directory, Widget, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_file_the_project_does_not_embed_alone()
    {
        // Arrange — the runtime reads no file the project leaves unembedded from its assembly.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "ES", "Components.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new CultureInAnotherCaseChecker(), Project(directory, Widget, new Dictionary<string, string>()), [file]);

        // Assert
        Assert.Empty(diagnostics);
    }

    private static IReadOnlyList<LintingDiagnostic> Check(TempDirectory directory, string source, params string[] files)
        => ProjectLinterTestFactory.Check(new CultureInAnotherCaseChecker(), Project(directory, source, Names(directory, files)), files);

    private static LoadedProject Project(TempDirectory directory, string source, IReadOnlyDictionary<string, string> resourceNames)
        => new(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced(source), false, false, EmbeddedResourceNames: resourceNames);

    // The build embeds each file under the root namespace and its path, every folder joined by a dot.
    private static Dictionary<string, string> Names(TempDirectory directory, string[] files)
        => files.ToDictionary(Path.GetFullPath, file => "Scratch." + Path.GetRelativePath(directory.Root, file).Replace(Path.DirectorySeparatorChar, '.'), StringComparer.OrdinalIgnoreCase);
}
