namespace Forma.Locali.Tests.Rules.Projects;

using Forma.Locali.Projects;
using Forma.Locali.Rules.Projects;

public sealed class LocalizationCoverageCheckerTests
{
    private static readonly string HebrewFile = Path.Combine("Widget", ".locales", "he.json");
    private static readonly string SpanishFile = Path.Combine("Widget", ".locales", "es.json");
    private static readonly string RegionalFile = Path.Combine("Widget", ".locales", "es-419.json");
    private static readonly string RootFile = Path.Combine(".locales", "he.json");

    [Fact]
    public async Task Should_report_a_key_the_locale_file_leaves_out()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(HebrewFile, LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = await LintAsync(directory, """public void Show() => _ = L["Tomorrow"];""", file);

        // Assert
        var diagnostic = Assert.Single(diagnostics);

        Assert.Multiple(
            () => Assert.Equal(LintingCodes.MissingLocalization, diagnostic.Code),
            () => Assert.Equal(LintingSeverity.Warning, diagnostic.Severity),
            () => Assert.Contains("'Tomorrow'", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains(HebrewFile, diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_leave_a_key_the_file_answers_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(HebrewFile, LocalizationFileFakeData.WithLocalizationEntry("Tomorrow", "מחר"));

        // Act
        var diagnostics = await LintAsync(directory, """public void Show() => _ = L["Tomorrow"];""", file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Should_match_an_escaped_key_the_way_the_runtime_reads_it()
    {
        // Arrange — the file and the callsite both write the escape, and the runtime drops it on both sides.
        using var directory = new TempDirectory();
        var file = directory.Write(HebrewFile, LocalizationFileFakeData.WithLocalizationEntry("$$price", "מחיר"));

        // Act
        var diagnostics = await LintAsync(directory, """public void Show() => _ = L["$$price"];""", file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Should_treat_a_key_written_as_null_as_answered()
    {
        // Arrange — null is how a file declares a key it leaves in the source language on purpose.
        using var directory = new TempDirectory();
        var file = directory.Write(HebrewFile, LocalizationFileFakeData.WithNullLocalizationEntry("Tomorrow"));

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
        var hebrew = directory.Write(HebrewFile, LocalizationFileFakeData.Valid);
        var spanish = directory.Write(SpanishFile, LocalizationFileFakeData.ForCulture("es", "Today", "hoy"));

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
        var hebrew = directory.Write(HebrewFile, LocalizationFileFakeData.Valid);
        var spanish = directory.Write(SpanishFile, LocalizationFileFakeData.ForCulture("es", "Tomorrow", "mañana"));

        // Act
        var diagnostics = await LintAsync(directory, """public void Show() => _ = L["Tomorrow"];""", hebrew, spanish);

        // Assert
        Assert.Contains(HebrewFile, Assert.Single(diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_let_a_parent_culture_answer_for_a_regional_file()
    {
        // Arrange — JsonStringLocale walks es-419 up to es, so a regional file need only carry what differs.
        using var directory = new TempDirectory();
        var regional = directory.Write(RegionalFile, LocalizationFileFakeData.ForCulture("es-419", "Today", "hoy"));
        var parent = directory.Write(SpanishFile, LocalizationFileFakeData.ForCulture("es", "Tomorrow", "mañana"));

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
        var regional = directory.Write(RegionalFile, LocalizationFileFakeData.ForCulture("es-419", "Tomorrow", "mañana"));
        var parent = directory.Write(SpanishFile, LocalizationFileFakeData.ForCulture("es", "Today", "hoy"));

        // Act
        var diagnostics = await LintAsync(directory, """public void Show() => _ = L["Tomorrow"];""", regional, parent);

        // Assert
        Assert.Contains(SpanishFile, Assert.Single(diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_report_a_key_written_at_several_sites_once_for_each_file()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(HebrewFile, LocalizationFileFakeData.Valid);

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
    public async Task Should_let_the_roots_file_answer_through_a_section()
    {
        // Arrange — the type's translations sit in the root's file, in a section of its own.
        using var directory = new TempDirectory();
        var file = directory.Write(RootFile, LocalizationFileFakeData.WithSection("Widget", "Tomorrow", "מחר"));

        // Act
        var diagnostics = await LintAsync(directory, """public void Show() => _ = L["Tomorrow"];""", file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Should_report_a_key_the_roots_file_leaves_out()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(RootFile, LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));

        // Act
        var diagnostics = await LintAsync(directory, """public void Show() => _ = L["Tomorrow"];""", file);

        // Assert
        Assert.Contains(RootFile, Assert.Single(diagnostics).Message, StringComparison.Ordinal);
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
        var file = directory.Write(HebrewFile, LocalizationFileFakeData.Valid);

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

    private static ValueTask<IReadOnlyList<LintingDiagnostic>> LintAsync(
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
            new SemanticCallsiteFinder(),
            new SyntacticCallsiteFinder(),
            SourceLinterTestFactory.Checkers());

        var sites = sources
            .Lint(project, [.. compilation.SyntaxTrees.Select(tree => tree.FilePath)])
            .Callsites;

        return ValueTask.FromResult(ProjectLinterTestFactory.Check(new LocalizationCoverageChecker(), project, localizationFiles, sites));
    }
}
