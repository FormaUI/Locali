namespace Forma.Locali.Tests.Sources;

using Forma.Locali.Projects;

public sealed class TranslationCoverageLinterTests
{
    private const string FileName = "Widget.he.json";

    [Fact]
    public async Task Should_report_a_key_the_translation_file_leaves_out()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(FileName, LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = await LintAsync(directory, """public void Show() => _ = L["Tomorrow"];""", file);

        // Assert
        var diagnostic = Assert.Single(diagnostics);

        Assert.Multiple(
            () => Assert.Equal(LintCodes.MissingTranslation, diagnostic.Code),
            () => Assert.Equal(LintSeverity.Warning, diagnostic.Severity),
            () => Assert.Contains("'Tomorrow'", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains(FileName, diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_leave_a_key_the_file_answers_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(FileName, LocalizationFileFakeData.WithTranslation("Tomorrow", "מחר"));

        // Act
        var diagnostics = await LintAsync(directory, """public void Show() => _ = L["Tomorrow"];""", file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Should_treat_a_key_written_as_null_as_answered()
    {
        // Arrange — null is how a file declares a key it leaves in the source language on purpose.
        using var directory = new TempDirectory();
        var file = directory.Write(FileName, LocalizationFileFakeData.WithNullTranslation("Tomorrow"));

        // Act
        var diagnostics = await LintAsync(directory, """public void Show() => _ = L["Tomorrow"];""", file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Should_report_once_for_every_culture_the_type_owns()
    {
        // Arrange
        using var directory = new TempDirectory();
        var hebrew = directory.Write(FileName, LocalizationFileFakeData.Valid);
        var spanish = directory.Write("Widget.es.json", LocalizationFileFakeData.ForCulture("es", "Today", "hoy"));

        // Act
        var diagnostics = await LintAsync(directory, """public void Show() => _ = L["Tomorrow"];""", hebrew, spanish);

        // Assert
        Assert.Equal(2, diagnostics.Count);
    }

    [Fact]
    public async Task Should_report_only_the_culture_that_leaves_the_key_out()
    {
        // Arrange
        using var directory = new TempDirectory();
        var hebrew = directory.Write(FileName, LocalizationFileFakeData.Valid);
        var spanish = directory.Write("Widget.es.json", LocalizationFileFakeData.ForCulture("es", "Tomorrow", "mañana"));

        // Act
        var diagnostics = await LintAsync(directory, """public void Show() => _ = L["Tomorrow"];""", hebrew, spanish);

        // Assert
        Assert.Contains(FileName, Assert.Single(diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_let_a_parent_culture_answer_for_a_regional_file()
    {
        // Arrange — JsonStringLocale walks es-419 up to es, so a regional file need only carry what differs.
        using var directory = new TempDirectory();
        var regional = directory.Write("Widget.es-419.json", LocalizationFileFakeData.ForCulture("es-419", "Today", "hoy"));
        var parent = directory.Write("Widget.es.json", LocalizationFileFakeData.ForCulture("es", "Tomorrow", "mañana"));

        // Act
        var diagnostics = await LintAsync(directory, """public void Show() => _ = L["Tomorrow"];""", regional, parent);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Should_not_let_a_regional_file_answer_for_its_parent()
    {
        // Arrange — the walk only ever goes up, so es cannot read what only es-419 carries.
        using var directory = new TempDirectory();
        var regional = directory.Write("Widget.es-419.json", LocalizationFileFakeData.ForCulture("es-419", "Tomorrow", "mañana"));
        var parent = directory.Write("Widget.es.json", LocalizationFileFakeData.ForCulture("es", "Today", "hoy"));

        // Act
        var diagnostics = await LintAsync(directory, """public void Show() => _ = L["Tomorrow"];""", regional, parent);

        // Assert
        Assert.Contains("Widget.es.json", Assert.Single(diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_report_a_key_written_at_several_sites_once_for_each_file()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(FileName, LocalizationFileFakeData.Valid);

        const string Body = """
            public void First() => _ = L["Tomorrow"];
            public void Second() => _ = L["Tomorrow"];
            """;

        // Act
        var diagnostics = await LintAsync(directory, Body, file);

        // Assert
        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Should_leave_a_type_that_owns_no_translations_alone()
    {
        // Arrange
        using var directory = new TempDirectory();

        // Act
        var diagnostics = await LintAsync(directory, """public void Show() => _ = L["Tomorrow"];""");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Should_ignore_a_lookup_whose_receiver_names_no_type()
    {
        // Arrange — the unbound contract says nothing about which file answers for the key.
        using var directory = new TempDirectory();
        var file = directory.Write(FileName, LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = await LintAsync(
            directory,
            """
            private IStringLocale Plain { get; } = default!;
            public void Show() => _ = Plain["Tomorrow"];
            """,
            file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Should_ignore_a_file_carrying_no_recognised_culture()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write("Widget.json", LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = await LintAsync(directory, """public void Show() => _ = L["Tomorrow"];""", file);

        // Assert
        Assert.Empty(diagnostics);
    }

    private static async ValueTask<IReadOnlyList<LintDiagnostic>> LintAsync(
        TempDirectory directory,
        string body,
        params string[] localizationFiles)
    {
        var compilation = CompilationTestFactory.Referenced($$"""
            using Forma.Framework.Localization;

            namespace Scratch;

            public sealed class Widget
            {
                private IStringLocale<Widget> L { get; } = default!;

                {{body}}
            }
            """);

        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), compilation, false, false);
        var sources = new SourceLinter(
            new SemanticCallSiteFinder(),
            new SyntacticCallSiteFinder(),
            SourceLinterTestFactory.Checkers());

        var sites = sources
            .Lint(project, [.. compilation.SyntaxTrees.Select(tree => tree.FilePath)])
            .CallSites;

        return await new TranslationCoverageLinter().LintAsync(
            TranslationFiles.Resolve(project, localizationFiles),
            sites,
            TestContext.Current.CancellationToken);
    }
}
