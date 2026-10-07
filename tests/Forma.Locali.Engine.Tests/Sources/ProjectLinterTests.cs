namespace Forma.Locali.Tests.Sources;

using Forma.Locali.Projects;

public sealed class ProjectLinterTests
{
    [Fact]
    public void Should_check_the_hosts_folder_of_a_project_that_did_not_compile()
    {
        // Arrange — no compilation leaves nothing to resolve the embedded files against, and the host's folder needs none.
        using var directory = new TempDirectory();
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), null, false, false, EmbeddedResourceNames: new Dictionary<string, string>());
        var nested = directory.Write(Path.Combine(".locales", "Contoso.Widgets", "Bars", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var flat = directory.Write(Path.Combine(".locales", "Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = new ProjectLinter(ProjectLinterTestFactory.Checkers(), LocaleFormatTestFactory.Json).Lint(project, [nested, flat], [], []);

        // Assert
        Assert.Equal([LintingCodes.DuplicateAnchorFile, LintingCodes.DuplicateAnchorFile], diagnostics.Codes());
    }

    [Fact]
    public void Should_report_a_refused_config_once_and_still_run_the_rules_that_do_not_read_it()
    {
        // Arrange — two configs for one assembly refuse every lookup, and the one beneath the root is misplaced besides.
        using var directory = new TempDirectory();
        var first = directory.Write(".locale.json", "{}");
        var second = directory.Write(Path.Combine("Pages", ".locale.json"), "{}");
        var file = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var resourceNames = new Dictionary<string, string>
        {
            [Path.GetFullPath(first)] = "Scratch..locale.json",
            [Path.GetFullPath(second)] = "Scratch.Pages..locale.json",
            [Path.GetFullPath(file)] = "Scratch.Components..locales.he.json",
        };
        var projectPath = Path.Combine(directory.Root, "Scratch.csproj");
        var project = new LoadedProject(projectPath, CompilationTestFactory.Unreferenced("namespace Scratch.Components; public sealed class Widget;"), false, false, EmbeddedResourceNames: resourceNames);

        // Act
        var diagnostics = new ProjectLinter(ProjectLinterTestFactory.Checkers(), LocaleFormatTestFactory.Json).Lint(project, [first, second, file], [], []);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([LintingCodes.MalformedLocaleConfig, LintingCodes.MalformedLocaleConfig], diagnostics.Codes()),
            () => Assert.Single(diagnostics, diagnostic => diagnostic.Path == projectPath),
            () => Assert.Single(diagnostics, diagnostic => diagnostic.Path == second));
    }
}
