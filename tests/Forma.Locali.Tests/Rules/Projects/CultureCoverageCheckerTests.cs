namespace Forma.Locali.Tests.Rules.Projects;

using Forma.Locali.Projects;
using Forma.Locali.Rules.Projects;

public sealed class CultureCoverageCheckerTests
{
    private const string Widget = "namespace Contoso.Widgets; public sealed class Widget;";

    [Fact]
    public void Should_report_a_type_translated_in_one_pack_culture_and_left_out_of_another()
    {
        // Arrange — Hebrew answers Widget, and the Spanish pack that also targets the project carries nothing for it.
        using var directory = new TempDirectory();
        var target = Target(directory);
        var heFile = directory.Write(Path.Combine("Packs", "Contoso.Widgets", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var esFile = directory.Write(Path.Combine("Packs", "Contoso.Widgets", "es.json"), LocalizationFileFakeData.ForCulture("es", "Today", "hoy"));
        var hePack = LocalePackTestFactory.Resolve(directory, "Hebrew", target, heFile);
        var esPack = LocalePackTestFactory.Resolve(directory, "Spanish", target, esFile);

        // Act
        var diagnostic = Assert.Single(ProjectLinterTestFactory.Check(new CultureCoverageChecker(), target, [], packs: [hePack, esPack]));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.MissingCultureCoverage, diagnostic.Code),
            () => Assert.Equal(heFile, diagnostic.Path),
            () => Assert.Contains("'Contoso.Widgets.Widget'", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains("'he'", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains("'es'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_leave_a_type_every_family_answers_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var target = Target(directory);
        var heFile = directory.Write(Path.Combine("Packs", "Contoso.Widgets", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var esFile = directory.Write(Path.Combine("Packs", "Contoso.Widgets", "es.json"), LocalizationFileFakeData.WithSection("es", "Widget", "Today", "hoy"));
        var hePack = LocalePackTestFactory.Resolve(directory, "Hebrew", target, heFile);
        var esPack = LocalePackTestFactory.Resolve(directory, "Spanish", target, esFile);

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new CultureCoverageChecker(), target, [], packs: [hePack, esPack]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_type_no_family_answers_alone()
    {
        // Arrange — Widget is unlocalized in both, which is unlocalized rather than uneven.
        using var directory = new TempDirectory();
        var target = Target(directory);
        var heFile = directory.Write(Path.Combine("Packs", "Contoso.Widgets", "he.json"), LocalizationFileFakeData.WithSection("Gadget", "Today", "היום"));
        var esFile = directory.Write(Path.Combine("Packs", "Contoso.Widgets", "es.json"), LocalizationFileFakeData.WithSection("es", "Gadget", "Today", "hoy"));
        var hePack = LocalePackTestFactory.Resolve(directory, "Hebrew", target, heFile);
        var esPack = LocalePackTestFactory.Resolve(directory, "Spanish", target, esFile);

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new CultureCoverageChecker(), target, [], packs: [hePack, esPack]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_stay_silent_with_only_one_culture_family_known()
    {
        // Arrange — Spanish never localizes this project at all, so there is nothing to compare Hebrew against.
        using var directory = new TempDirectory();
        var target = Target(directory);
        var heFile = directory.Write(Path.Combine("Packs", "Contoso.Widgets", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var hePack = LocalePackTestFactory.Resolve(directory, target, heFile);

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new CultureCoverageChecker(), target, [], packs: [hePack]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_regional_culture_answered_by_its_family_alone()
    {
        // Arrange — the Spanish family is answered by a regional override alone, with no plain 'es' file at all, so it is not missing.
        using var directory = new TempDirectory();
        var target = Target(directory);
        var heFile = directory.Write(Path.Combine("Packs", "Contoso.Widgets", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var esFile = directory.Write(Path.Combine("Packs", "Contoso.Widgets", "es-419.json"), LocalizationFileFakeData.WithSection("es-419", "Widget", "Today", "hoy"));
        var hePack = LocalePackTestFactory.Resolve(directory, "Hebrew", target, heFile);
        var esPack = LocalePackTestFactory.Resolve(directory, "Spanish", target, esFile);

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new CultureCoverageChecker(), target, [], packs: [hePack, esPack]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_stay_silent_when_the_project_did_not_compile()
    {
        // Arrange
        using var directory = new TempDirectory();
        var target = new LoadedProject(Path.Combine(directory.Root, "Widgets", "Contoso.Widgets.csproj"), null, false, false, EmbeddedResourceNames: new Dictionary<string, string>());
        var heFile = directory.Write(Path.Combine("Packs", "Contoso.Widgets", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var hePack = LocalePackTestFactory.Resolve(directory, target, heFile);

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new CultureCoverageChecker(), target, [], packs: [hePack]);

        // Assert
        Assert.Empty(diagnostics);
    }

    private static LoadedProject Target(TempDirectory directory)
        => new(Path.Combine(directory.Root, "Widgets", "Contoso.Widgets.csproj"), CompilationTestFactory.Building("Contoso.Widgets", Widget), false, false, EmbeddedResourceNames: new Dictionary<string, string>());
}
