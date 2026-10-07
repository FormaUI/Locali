namespace Forma.Locali.Tests.Rules.Projects;

using Forma.Locali.Projects;
using Forma.Locali.Rules.Projects;

public sealed class LocaleSourceCheckerTests
{
    [Fact]
    public void Should_report_an_embedded_file_where_the_configuration_allows_packs_and_hosts_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "localeSources": { "**": ["pack", "host"] } }""");
        var file = directory.Write(Path.Combine("Bars", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var project = Project(directory, embedded: [file]);

        // Act
        var diagnostic = Assert.Single(Check(project, file));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.DisallowedLocaleSource, diagnostic.Code),
            () => Assert.Equal(file, diagnostic.Path),
            () => Assert.Contains("embedded in the assembly", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains("naming pack and host", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_report_a_satellites_file_apart_from_an_embedded_one()
    {
        // Arrange — the build moves the file into a satellite assembly, which the configuration allows nothing but embedding for.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "localeSources": { "**": ["embedded"] } }""");
        var file = directory.Write(Path.Combine("Bars", ".locales", "Widget.he.json"), LocalizationFileFakeData.Valid);
        var project = Project(directory, embedded: [file], satellite: [file]);

        // Act
        var diagnostic = Assert.Single(Check(project, file));

        // Assert
        Assert.Contains("embedded in a satellite assembly", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_report_a_hosts_file_where_the_configuration_allows_none()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "localeSources": { "**": ["embedded", "pack"] } }""");
        var file = directory.Write(Path.Combine(".locales", "Contoso.Widgets", "he.json"), LocalizationFileFakeData.Valid);
        var project = Project(directory, embedded: []);

        // Act
        var diagnostic = Assert.Single(Check(project, file));

        // Assert
        Assert.Contains("laid in the host's folder", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_report_a_packs_file_where_the_configuration_allows_none()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "localeSources": { "**": ["embedded"] } }""");
        var file = directory.Write(Path.Combine("Contoso.Widgets", "he.json"), LocalizationFileFakeData.Valid);
        var pack = new LoadedProject(Path.Combine(directory.Root, "Contoso.Locales.Hebrew.csproj"), CompilationTestFactory.Building("Contoso.Locales.Hebrew"), false, false, EmbeddedResourceNames: Names(directory, file), IsLocalePack: true);

        // Act
        var diagnostic = Assert.Single(Check(pack, file));

        // Assert
        Assert.Contains("carried by a locale pack", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_report_a_packs_file_for_a_project_whose_line_allows_embedding_alone()
    {
        // Arrange — the add-on embeds its own translations, so a pack's folder for it keeps them where the repository said they are not.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "localeSources": { "**": ["pack", "host"], "AddOns/**": ["embedded"] } }""");
        var target = Target(directory, Path.Combine("AddOns", "Contoso.Office"), "Contoso.Office");
        var file = directory.Write(Path.Combine(LocalePackTestFactory.Folder, "Contoso.Office", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostic = Assert.Single(CheckPack(directory, target, file));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(file, diagnostic.Path),
            () => Assert.Contains("carried by a locale pack", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains("matching it as a file of Contoso.Office.csproj, the project it localizes,", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains("naming embedded", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_leave_a_packs_file_alone_where_the_line_over_its_project_allows_packs()
    {
        // Arrange — the line over the pack's own folder allows embedding alone, and the line over the project the file localizes allows packs.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "localeSources": { "**": ["embedded"], "Widgets/**": ["pack"] } }""");
        var target = Target(directory, "Widgets", "Contoso.Widgets");
        var file = directory.Write(Path.Combine(LocalePackTestFactory.Folder, "Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = CheckPack(directory, target, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_judge_a_packs_file_by_where_it_sits_when_its_folder_names_no_project_of_the_run()
    {
        // Arrange — no project of the run builds the assembly the folder names, so the line over the pack's own folder speaks for the file.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "localeSources": { "**": ["embedded"], "Widgets/**": ["pack"] } }""");
        var target = Target(directory, "Widgets", "Contoso.Widgets");
        var file = directory.Write(Path.Combine(LocalePackTestFactory.Folder, "Contoso.Gadgets", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostic = Assert.Single(CheckPack(directory, target, file));

        // Assert
        Assert.Contains("matching it does not allow", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_leave_a_file_from_an_allowed_source_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "localeSources": { "**": ["embedded", "host"] } }""");
        var embedded = directory.Write(Path.Combine("Bars", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var host = directory.Write(Path.Combine(".locales", "Contoso.Widgets", "he.json"), LocalizationFileFakeData.Valid);
        var project = Project(directory, embedded: [embedded]);

        // Act
        var diagnostics = Check(project, embedded, host);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_every_file_alone_when_no_configuration_names_sources()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Bars", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var project = Project(directory, embedded: [file]);

        // Act
        var diagnostics = Check(project, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_let_a_later_line_restate_the_sources_for_a_subtree()
    {
        // Arrange — the general line allows packs and hosts alone, and the later line for the application allows embedding again.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "localeSources": { "**": ["pack", "host"], "Showcase/**": ["embedded", "pack", "host"] } }""");
        var file = directory.Write(Path.Combine("Showcase", "Pages", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var project = new LoadedProject(Path.Combine(directory.Root, "Showcase", "Showcase.csproj"), CompilationTestFactory.Building("Showcase"), false, false, EmbeddedResourceNames: Names(directory, file));

        // Act
        var diagnostics = Check(project, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_stay_silent_when_the_projects_items_were_not_read_and_still_count_the_entry_as_applied()
    {
        // Arrange — without the items no file is known to be embedded, so where it comes from cannot be told; the entry still governed a file, so it is not stale.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "localeSources": { "**": ["pack"] } }""");
        var file = directory.Write(Path.Combine("Bars", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var project = new LoadedProject(Path.Combine(directory.Root, "Contoso.Widgets.csproj"), CompilationTestFactory.Building("Contoso.Widgets"), false, false);
        var configuration = new LintingConfiguration(directory.Root);

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new LocaleSourceChecker(configuration), project, [file]);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(diagnostics),
            () => Assert.Empty(configuration.GetDiagnostics()));
    }

    [Fact]
    public void Should_leave_a_file_the_project_neither_embeds_nor_hosts_alone()
    {
        // Arrange — a locale file the runtime never reads comes from no source at all, and is another rule's to notice.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "localeSources": { "**": ["pack"] } }""");
        var file = directory.Write(Path.Combine("Bars", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var project = Project(directory, embedded: []);

        // Act
        var diagnostics = Check(project, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    private static IReadOnlyList<LintingDiagnostic> Check(LoadedProject project, params string[] files)
        => ProjectLinterTestFactory.Check(new LocaleSourceChecker(new LintingConfiguration(project.Path)), project, files);

    private static IReadOnlyList<LintingDiagnostic> CheckPack(TempDirectory directory, LoadedProject target, string file)
    {
        var pack = LocalePackTestFactory.Resolve(directory, target, file);

        return ProjectLinterTestFactory.Check(new LocaleSourceChecker(new LintingConfiguration(directory.Root)), pack.Project, [file], packs: [pack]);
    }

    private static LoadedProject Target(TempDirectory directory, string folder, string assemblyName)
        => new(Path.Combine(directory.Root, folder, $"{assemblyName}.csproj"), CompilationTestFactory.Building(assemblyName), false, false, EmbeddedResourceNames: new Dictionary<string, string>());

    private static LoadedProject Project(TempDirectory directory, IReadOnlyList<string> embedded, IReadOnlyList<string>? satellite = null)
        => new(
            Path.Combine(directory.Root, "Contoso.Widgets.csproj"),
            CompilationTestFactory.Building("Contoso.Widgets"),
            false,
            false,
            EmbeddedResourceNames: Names(directory, [.. embedded]),
            SatelliteFiles: satellite?.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase));

    private static Dictionary<string, string> Names(TempDirectory directory, params string[] files)
        => files.ToDictionary(
            Path.GetFullPath,
            file => "Contoso.Widgets." + Path.GetRelativePath(directory.Root, file).Replace(Path.DirectorySeparatorChar, '.').Replace(Path.AltDirectorySeparatorChar, '.'),
            StringComparer.OrdinalIgnoreCase);
}
