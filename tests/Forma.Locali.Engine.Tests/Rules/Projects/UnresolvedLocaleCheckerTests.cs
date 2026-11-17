namespace Forma.Locali.Tests.Rules.Projects;

using Forma.Locali.Projects;
using Forma.Locali.Rules.Projects;

public sealed class UnresolvedLocaleCheckerTests
{
    private static readonly string HebrewFile = Path.Combine("Widget", ".locales", "he.json");

    [Fact]
    public void Should_report_a_lookup_no_path_reaches_a_type_for()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(HebrewFile, LocalizationFileFakeData.Valid);

        // Act
        var diagnostic = Assert.Single(Check(directory, """
            internal sealed class Factory : IStringLocaleFactory
            {
                public IStringLocale Create(Type resourceSource) => throw new NotSupportedException();

                public IStringLocale Create(string baseName, string location) => throw new NotSupportedException();
            }

            public sealed class Widget
            {
                public string Show() => new Factory().Create(typeof(Widget))["Tomorrow"].Value;
            }
            """, [], file));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.UnresolvedLocale, diagnostic.Code),
            () => Assert.Equal(LintingSeverity.Warning, diagnostic.Severity),
            () => Assert.Equal(
                "No path from the lookup reaches the type its locale is read for, ending at a factory the code builds itself, 'new Factory().Create(typeof(Widget))', so no rule could check whether a culture answers 'Tomorrow'.",
                diagnostic.Message));
    }

    [Fact]
    public void Should_leave_a_lookup_reaching_a_type_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(HebrewFile, LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Check(directory, """
            public sealed class Widget(IStringLocale<Widget> locale)
            {
                public string Show() => locale["Tomorrow"].Value;
            }
            """, [], file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_project_localized_in_no_culture_alone()
    {
        // Arrange — a project localizing nothing has no key to check, through any type.
        using var directory = new TempDirectory();

        // Act
        var diagnostics = Check(directory, """
            internal sealed class Factory : IStringLocaleFactory
            {
                public IStringLocale Create(Type resourceSource) => throw new NotSupportedException();

                public IStringLocale Create(string baseName, string location) => throw new NotSupportedException();
            }

            public sealed class Widget
            {
                public string Show() => new Factory().Create(typeof(Widget))["Tomorrow"].Value;
            }
            """, []);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_lookup_whose_hinted_type_member_tells_no_type_to_LOC0214()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(HebrewFile, LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Check(directory, """
            public abstract class Component
            {
                protected virtual Type LocalizationSourceType => GetType();

                protected IStringLocale L => default!;
            }

            public sealed class Widget : Component
            {
                private readonly Type[] _sources = [typeof(Widget)];

                protected override Type LocalizationSourceType => _sources[0];

                public string Show() => L["Tomorrow"].Value;
            }
            """, [LocaleFlowTestFactory.Hint("Scratch", "Scratch.Component")], file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_lookup_no_path_was_followed_from_alone()
    {
        // Arrange — no instance of the project runs the abstract type's code, so no path ended anywhere to name.
        using var directory = new TempDirectory();
        var file = directory.Write(HebrewFile, LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Check(directory, """
            public abstract class Widget
            {
                protected IStringLocale L => default!;

                public string Show() => L["Tomorrow"].Value;
            }
            """, [], file);

        // Assert
        Assert.Empty(diagnostics);
    }

    private static IReadOnlyList<LintingDiagnostic> Check(TempDirectory directory, string declarations, IReadOnlyList<LocaleMemberHint> hints, params string[] localizationFiles)
    {
        var compilation = CompilationTestFactory.Referenced($$"""
            using System;

            using Forma.Framework.Localization;

            namespace Scratch;

            {{declarations}}
            """);
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), compilation, false, false);
        var sites = new SourceLinter(SourceLinterTestFactory.Finders(), SourceLinterTestFactory.Checkers())
            .Lint(project, [.. compilation.SyntaxTrees.Select(tree => tree.FilePath)], TestContext.Current.CancellationToken)
            .Callsites;

        return ProjectLinterTestFactory.Check(new UnresolvedLocaleChecker(), project, localizationFiles, new LocaleFlow([project], hints, []).Resolve(project, sites));
    }
}
