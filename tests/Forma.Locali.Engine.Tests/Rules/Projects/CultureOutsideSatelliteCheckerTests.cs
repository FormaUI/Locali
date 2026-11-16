namespace Forma.Locali.Tests.Rules.Projects;

using Forma.Locali.Projects;
using Forma.Locali.Rules.Projects;

public sealed class CultureOutsideSatelliteCheckerTests
{
    [Fact]
    public void Should_report_a_file_a_satellite_holds_under_another_cultures_name()
    {
        // Arrange — the 'he' satellite holds the file under a name whose culture is 'es', which no culture's reading lists.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "es", "Widget.he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostic = Assert.Single(Check(directory, file, "Scratch/.locales/es/Widget.he.json", "he"));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.CultureOutsideSatellite, diagnostic.Code),
            () => Assert.Equal(file, diagnostic.Path),
            () => Assert.Equal(
                "The 'he' satellite assembly holds this file under 'Scratch/.locales/es/Widget.he.json', whose culture is 'es', and the runtime reads no file a satellite holds under another culture's name, so none of its entries is read.",
                diagnostic.Message));
    }

    [Fact]
    public void Should_leave_a_file_a_satellite_holds_under_its_own_cultures_name_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "he", "Widget.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Check(directory, file, "Scratch/.locales/he/Widget.json", "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_satellites_file_whose_name_holds_no_culture_alone()
    {
        // Arrange — the runtime reads the satellite's culture into such a name.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Strings", "Widget.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Check(directory, file, "Scratch/Strings/Widget.json", "he");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_file_no_satellite_holds_alone()
    {
        // Arrange — the assembly itself holds the file, which is read for the culture its name gives it.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "es", "Widget.he.json"), LocalizationFileFakeData.ForCulture("es", "Today", "Hoy"));

        // Act
        var diagnostics = Check(directory, file, "Scratch/.locales/es/Widget.he.json", satelliteCulture: null);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_project_whose_items_were_not_read_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "es", "Widget.he.json"), LocalizationFileFakeData.Valid);
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced("namespace Scratch;"), false, false);

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new CultureOutsideSatelliteChecker(), project, [file]);

        // Assert
        Assert.Empty(diagnostics);
    }

    private static IReadOnlyList<LintingDiagnostic> Check(TempDirectory directory, string file, string name, string? satelliteCulture)
    {
        var fullPath = Path.GetFullPath(file);
        var project = new LoadedProject(
            Path.Combine(directory.Root, "Scratch.csproj"),
            CompilationTestFactory.Unreferenced("namespace Scratch;"),
            false,
            false,
            EmbeddedResourceNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [fullPath] = name },
            SatelliteCultures: satelliteCulture is null ? new Dictionary<string, string>() : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [fullPath] = satelliteCulture });

        return ProjectLinterTestFactory.Check(new CultureOutsideSatelliteChecker(), project, [file]);
    }
}
