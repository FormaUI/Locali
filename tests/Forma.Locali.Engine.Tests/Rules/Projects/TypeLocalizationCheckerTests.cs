namespace Forma.Locali.Tests.Rules.Projects;

using Forma.Locali.Projects;
using Forma.Locali.Rules.Projects;

using Microsoft.CodeAnalysis;

public sealed class TypeLocalizationCheckerTests
{
    private static readonly string WidgetFile = Path.Combine("Widget", ".locales", "he.json");
    private static readonly string GadgetFile = Path.Combine("Gadget", ".locales", "he.json");
    private static readonly string SpanishGadgetFile = Path.Combine("Gadget", ".locales", "es.json");

    [Fact]
    public void Should_report_a_type_lookups_read_that_no_culture_its_assembly_carries_localizes()
    {
        // Arrange — the project localizes Gadget in Hebrew, and Widget nowhere.
        using var directory = new TempDirectory();
        var file = directory.Write(GadgetFile, LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Check(directory, """public void Show() => _ = L["Tomorrow"];""", file);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.UnlocalizedType, diagnostic.Code),
            () => Assert.Equal(LintingSeverity.Warning, diagnostic.Severity),
            () => Assert.Equal(
                "'Scratch.Widget', the type the lookup reads through IStringLocale<Widget>, has no entries in 'he', a culture Scratch's locale files carry, so every key read for it renders in the source language there.",
                diagnostic.Message));
    }

    [Fact]
    public void Should_report_once_for_every_culture_the_assembly_carries()
    {
        // Arrange
        using var directory = new TempDirectory();
        var hebrew = directory.Write(GadgetFile, LocalizationFileFakeData.Valid);
        var spanish = directory.Write(SpanishGadgetFile, LocalizationFileFakeData.ForCulture("es", "Today", "hoy"));

        // Act
        var diagnostics = Check(directory, """public void Show() => _ = L["Tomorrow"];""", hebrew, spanish);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(2, diagnostics.Count),
            () => Assert.Contains(diagnostics, diagnostic => diagnostic.Message.Contains("no entries in 'he'", StringComparison.Ordinal)),
            () => Assert.Contains(diagnostics, diagnostic => diagnostic.Message.Contains("no entries in 'es'", StringComparison.Ordinal)));
    }

    [Fact]
    public void Should_report_a_type_read_at_several_sites_once()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(GadgetFile, LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Check(
            directory,
            """
            public void First() => _ = L["Tomorrow"];
            public void Second() => _ = L["Yesterday"];
            """,
            file);

        // Assert
        Assert.Single(diagnostics);
    }

    [Fact]
    public void Should_leave_a_type_localized_in_another_culture_alone()
    {
        // Arrange — Widget has Hebrew entries, so the culture it leaves out is LOC0119's to report.
        using var directory = new TempDirectory();
        var hebrew = directory.Write(WidgetFile, LocalizationFileFakeData.Valid);
        var spanish = directory.Write(SpanishGadgetFile, LocalizationFileFakeData.ForCulture("es", "Today", "hoy"));

        // Act
        var diagnostics = Check(directory, """public void Show() => _ = L["Tomorrow"];""", hebrew, spanish);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_type_only_a_shared_block_above_it_reaches_in_each_culture_the_block_leaves_out()
    {
        // Arrange — the root's Hebrew shared block answers for every type beneath it, and nothing gives Widget a Spanish entry.
        using var directory = new TempDirectory();
        var hebrew = directory.Write(Path.Combine(".locales", "he.json"), LocalizationFileFakeData.WithSharedAndSection("Today", "היום", "Gadget", "Today", "היום"));
        var spanish = directory.Write(SpanishGadgetFile, LocalizationFileFakeData.ForCulture("es", "Today", "hoy"));

        // Act
        var diagnostics = Check(directory, """public void Show() => _ = L["Tomorrow"];""", hebrew, spanish);

        // Assert
        Assert.Contains("no entries in 'es'", Assert.Single(diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_leave_a_type_its_own_shared_block_localizes_alone()
    {
        // Arrange — a shared block in Widget's own section is Widget's own, so the culture it leaves out is LOC0119's to report.
        using var directory = new TempDirectory();
        var hebrew = directory.Write(Path.Combine(".locales", "he.json"), LocalizationFileFakeData.WithSharedSection("Widget", "Today", "היום"));
        var spanish = directory.Write(SpanishGadgetFile, LocalizationFileFakeData.ForCulture("es", "Today", "hoy"));

        // Act
        var diagnostics = Check(directory, """public void Show() => _ = L["Tomorrow"];""", hebrew, spanish);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_type_a_shared_block_gives_entries_in_every_culture_alone()
    {
        // Arrange — the root's shared block answers for every type beneath it, so Widget reads entries and its keys are LOC0207's.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "he.json"), LocalizationFileFakeData.WithSharedAndSection("Today", "היום", "Gadget", "Today", "היום"));

        // Act
        var diagnostics = Check(directory, """public void Show() => _ = L["Tomorrow"];""", file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_type_alone_where_its_assembly_carries_no_locale_files()
    {
        // Arrange
        using var directory = new TempDirectory();

        // Act
        var diagnostics = Check(directory, """public void Show() => _ = L["Tomorrow"];""");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_ignore_a_lookup_whose_receiver_names_no_type()
    {
        // Arrange — the unbound contract says nothing about which type's entries answer the key.
        using var directory = new TempDirectory();
        var file = directory.Write(GadgetFile, LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Check(
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
    public void Should_leave_a_type_reached_through_alternatives_alone()
    {
        // Arrange — the lookup reads one of two types, and the flow cannot tell which.
        using var directory = new TempDirectory();
        var file = directory.Write(GadgetFile, LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Check(
            directory,
            """
            private IStringLocale<Gadget> Other { get; } = default!;
            public void Show(bool flag) => _ = (flag ? (IStringLocale)L : Other)["Tomorrow"];
            """,
            file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_name_the_hint_a_type_only_a_hint_supplied_rests_on()
    {
        // Arrange — the component's locale is opaque to the flow, so the hint relating it to its type member names the type.
        using var directory = new TempDirectory();
        var file = directory.Write(GadgetFile, LocalizationFileFakeData.Valid);
        var compilation = CompilationTestFactory.Referenced("""
            using System;

            using Forma.Framework.Localization;

            namespace Scratch;

            public class Widget
            {
                protected virtual Type LocalizationSourceType => GetType();

                protected IStringLocale L => default!;

                public void Show() => _ = L["Tomorrow"];
            }

            public sealed class Gadget;
            """);
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), compilation, false, false);

        // Act
        var diagnostics = Check(project, [LocaleFlowTestFactory.Hint("Scratch", "Scratch.Widget")], file);

        // Assert
        Assert.Contains($"by the hint at '{LocaleFlowTestFactory.HintPath}:1'", Assert.Single(diagnostics).Message, StringComparison.Ordinal);
    }

    // Widget's lookups over the project's files, with Gadget beside it for the files to localize instead.
    private static IReadOnlyList<LintingDiagnostic> Check(TempDirectory directory, string body, params string[] localizationFiles)
    {
        var compilation = CompilationTestFactory.Referenced($$"""
            using Forma.Framework.Localization;

            namespace Scratch;

            public sealed class Widget
            {
                private IStringLocale<Widget> L { get; } = default!;

                {{body}}
            }

            public sealed class Gadget;
            """);

        return Check(new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), compilation, false, false), [], localizationFiles);
    }

    // The project's callsites as the source linter finds them, each followed to its types as a run does.
    private static IReadOnlyList<LintingDiagnostic> Check(LoadedProject project, IReadOnlyList<LocaleMemberHint> hints, params string[] localizationFiles)
    {
        var sites = new SourceLinter(SourceLinterTestFactory.Finders(), SourceLinterTestFactory.Checkers())
            .Lint(project, [.. project.Compilation!.SyntaxTrees.Select(tree => tree.FilePath)], TestContext.Current.CancellationToken)
            .Callsites;

        return ProjectLinterTestFactory.Check(new TypeLocalizationChecker(), project, localizationFiles, new LocaleFlow([project], hints, []).Resolve(project, sites));
    }
}
