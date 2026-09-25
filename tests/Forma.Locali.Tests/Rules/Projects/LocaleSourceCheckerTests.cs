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
