namespace Forma.Locali.Tests.Rules.Projects;

using Forma.Locali.Projects;
using Forma.Locali.Rules.Projects;

public sealed class UnreadLocaleSourceCheckerTests
{
    private static readonly string HebrewFile = Path.Combine(".locales", "Widget.he.json");

    [Fact]
    public void Should_report_a_lookup_whose_named_property_returns_a_type_from_an_array()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(HebrewFile, LocalizationFileFakeData.Valid);

        // Act
        var diagnostic = Assert.Single(Check(directory, """
            private readonly Type[] _sources = [typeof(Widget)];

            protected override Type LocalizationSourceType => _sources[0];
            """, file));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.UnreadLocaleSource, diagnostic.Code),
            () => Assert.Equal(LintingSeverity.Warning, diagnostic.Severity),
            () => Assert.Equal(
                "'Widget.LocalizationSourceType for Widget' names the type 'Tomorrow' is read in, and its getter returns no typeof or GetType() that could be read, so no rule could check the key there.",
                diagnostic.Message));
    }

    [Fact]
    public void Should_leave_a_lookup_whose_named_property_returns_a_type_through_typeof_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(HebrewFile, LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Check(directory, "protected override Type LocalizationSourceType => typeof(Widget);", file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_lookup_whose_named_property_returns_the_instances_GetType_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(HebrewFile, LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Check(directory, string.Empty, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_project_with_no_locale_files_alone()
    {
        // Arrange — a project localizing nothing has no key to check, read through any type.
        using var directory = new TempDirectory();

        // Act
        var diagnostics = Check(directory, """
            private readonly Type[] _sources = [typeof(Widget)];

            protected override Type LocalizationSourceType => _sources[0];
            """);

        // Assert
        Assert.Empty(diagnostics);
    }

    private static IReadOnlyList<LintingDiagnostic> Check(TempDirectory directory, string members, params string[] localizationFiles)
    {
        var compilation = CompilationTestFactory.Referenced($$"""
            using System;

            using Forma.Framework.Localization;

            namespace Scratch;

            public abstract class Component
            {
                protected virtual Type LocalizationSourceType => GetType();

                [LocaleSource(nameof(LocalizationSourceType))]
                protected IStringLocale L => default!;
            }

            public sealed class Widget : Component
            {
                {{members}}

                public void Show() => _ = L["Tomorrow"];
            }
            """);
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), compilation, false, false);
        var sites = new SourceLinter(SourceLinterTestFactory.Finders(), SourceLinterTestFactory.Checkers())
            .Lint(project, [.. compilation.SyntaxTrees.Select(tree => tree.FilePath)], TestContext.Current.CancellationToken)
            .Callsites;

        return ProjectLinterTestFactory.Check(new UnreadLocaleSourceChecker(), project, localizationFiles, new LocaleFlow([project], LocaleFlow.ReadLocaleSourceAttribute).Resolve(project, sites));
    }
}
