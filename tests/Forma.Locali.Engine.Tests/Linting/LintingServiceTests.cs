namespace Forma.Locali.Tests.Linting;

using Forma.Locali.Projects;

using Microsoft.CodeAnalysis;

public sealed class LintingServiceTests
{
    [Fact]
    public async Task Should_report_nothing_for_a_file_that_follows_the_schema()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Valid);

        // Act
        var result = await LintingServiceTestFactory.Create(file).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task Should_report_a_file_in_a_folder_of_locale_files_that_carries_no_marker()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", LocalizationFileFakeData.HebrewName), LocalizationFileFakeData.Unmarked);

        // Act
        var result = await LintingServiceTestFactory.Create(file).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([LintingCodes.MissingMarker], result.Diagnostics.Codes());
    }

    [Fact]
    public async Task Should_ignore_a_file_in_a_culture_folder_outside_any_folder_of_locale_files_that_carries_no_marker()
    {
        // Arrange — the build embeds a file outside a folder of locale files only where it carries the marker.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("wwwroot", "i18n", "he", "strings.json"), LocalizationFileFakeData.Unmarked);

        // Act
        var result = await LintingServiceTestFactory.Create(file).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task Should_ignore_json_that_names_no_culture_and_carries_no_marker()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write("appsettings.json", """{ "Logging": { "Level": "Debug" } }""");

        // Act
        var result = await LintingServiceTestFactory.Create(file).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task Should_search_a_directory_recursively()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write($"Nested/Deeper/.locales/{LocalizationFileFakeData.HebrewName}", LocalizationFileFakeData.Unmarked);

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([LintingCodes.MissingMarker], result.Diagnostics.Codes());
    }

    [Fact]
    public async Task Should_ignore_build_output()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write($"bin/Debug/{LocalizationFileFakeData.HebrewName}", LocalizationFileFakeData.Unmarked);
        directory.Write($"obj/{LocalizationFileFakeData.HebrewName}", LocalizationFileFakeData.Unmarked);

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task Should_report_a_file_that_is_not_readable_json()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", LocalizationFileFakeData.HebrewName), LocalizationFileFakeData.Unreadable);

        // Act
        var result = await LintingServiceTestFactory.Create(file).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([LintingCodes.InvalidJson], result.Diagnostics.Codes());
    }

    [Fact]
    public async Task Should_count_every_file_it_read()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Valid);
        directory.Write("Nested/Other.he.json", LocalizationFileFakeData.Valid);

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.FileCount);
    }

    [Fact]
    public async Task Should_order_diagnostics_by_file()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write("Second.he.json", LocalizationFileFakeData.Unmarked);
        directory.Write("First.he.json", LocalizationFileFakeData.Unmarked);

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([.. result.Diagnostics.Paths().Order(StringComparer.Ordinal)], result.Diagnostics.Paths());
    }

    [Fact]
    public async Task Should_leave_a_warning_out_when_the_run_reports_errors_only()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.TrailingMarker);

        // Act
        var result = await LintingServiceTestFactory.Create(file).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(result.Diagnostics),
            () => Assert.Equal(1, result.SuppressedCount));
    }

    [Fact]
    public async Task Should_report_a_warning_when_the_run_reports_warnings()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.TrailingMarker);

        // Act
        var result = await LintingServiceTestFactory.Create(file, LintingSeverity.Warning)
            .RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([LintingCodes.MarkerNotFirst], result.Diagnostics.Codes()),
            () => Assert.Equal(0, result.SuppressedCount));
    }

    [Fact]
    public async Task Should_leave_out_but_still_count_a_diagnostic_the_runs_own_configuration_ignores()
    {
        // Arrange — two sections word one key two ways, which the linting configuration says is meant.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "Widget.he.json": ["LOC0113"] } }""");
        directory.Write("Widget.he.json", LocalizationFileFakeData.WithSections(("A", "Today", "היום"), ("B", "Today", "עכשיו")));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.DoesNotContain(LintingCodes.InconsistentLocalization, result.Diagnostics.Codes()),
            () => Assert.Equal(1, result.SuppressedCount),
            () => Assert.Equal(1, result.IgnoredCount));
    }

    [Fact]
    public async Task Should_report_the_runs_own_configuration_as_malformed()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", "not json");
        directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Valid);

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([LintingCodes.MalformedConfiguration], result.Diagnostics.Codes());
    }

    [Fact]
    public async Task Should_report_a_stale_glob_only_at_warning_severity()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": { "Nowhere.he.json": ["LOC0113"] } }""");
        directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Valid);

        // Act
        var errorsOnly = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);
        var warnings = await LintingServiceTestFactory.Create(directory.Root, LintingSeverity.Warning)
            .RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(errorsOnly.Diagnostics),
            () => Assert.Equal([LintingCodes.StaleConfigurationEntry], warnings.Diagnostics.Codes()));
    }

    [Fact]
    public async Task Should_report_a_configuration_file_the_run_does_not_read()
    {
        // Arrange — the root's file is the one read, so the nested one's exclusion does not apply and the file itself is reported.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "excludeFiles": ["Fixtures/*"] }""");
        var nested = directory.Write(Path.Combine("Widgets", ".locali.json"), """{ "excludeFiles": ["*.he.json"] }""");
        directory.Write(Path.Combine("Fixtures", ".locales", LocalizationFileFakeData.HebrewName), LocalizationFileFakeData.Unmarked);
        var file = directory.Write(Path.Combine("Widgets", ".locales", LocalizationFileFakeData.HebrewName), LocalizationFileFakeData.Unmarked);

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, LintingSeverity.Warning).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([LintingCodes.UnreadConfiguration, LintingCodes.MissingMarker], result.Diagnostics.Codes().OrderBy(code => code.Id, StringComparer.Ordinal)),
            () => Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Path == file),
            () => Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Path == nested && diagnostic.Code == LintingCodes.UnreadConfiguration));
    }

    [Fact]
    public async Task Should_exclude_a_file_the_runs_own_configuration_names()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "excludeFiles": ["*.he.json"] }""");
        directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Unmarked);

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(result.Diagnostics),
            () => Assert.Equal(0, result.FileCount));
    }

    [Fact]
    public async Task Should_report_an_embedded_config_marking_two_roots_and_finish_the_run()
    {
        // Arrange — a second config file among an assembly's files is refused for the whole assembly, and the misplaced one is reported where it sits.
        using var directory = new TempDirectory();
        var project = directory.Write("Scratch.csproj", """<Project Sdk="Microsoft.NET.Sdk" />""");
        var first = directory.Write(".locale.json", "{}");
        var second = directory.Write(Path.Combine("Pages", ".locale.json"), "{}");
        var file = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var resourceNames = new Dictionary<string, string>
        {
            [Path.GetFullPath(first)] = "Scratch/.locale.json",
            [Path.GetFullPath(second)] = "Scratch/Pages/.locale.json",
            [Path.GetFullPath(file)] = "Scratch/Components/.locales/he.json",
        };
        var compilation = CompilationTestFactory.Unreferenced("namespace Scratch.Components; public sealed class Widget;");
        var loader = new StubProjectLoader(new LoadedProject(project, compilation, false, false, EmbeddedResourceNames: resourceNames));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, projectLoader: loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics, candidate => candidate.Path == project);
        Assert.Multiple(
            () => Assert.Equal([LintingCodes.MalformedLocaleConfig, LintingCodes.MalformedLocaleConfig], result.Diagnostics.Codes()),
            () => Assert.Contains(result.Diagnostics, candidate => candidate.Path == second),
            () => Assert.Contains(Path.GetFullPath(first), diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains(Path.GetFullPath(second), diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_report_a_locale_config_the_runtime_would_refuse()
    {
        // Arrange — the runtime's own config file sits among the locale files and is held to the runtime's rule.
        using var directory = new TempDirectory();
        var config = directory.Write(".locale.json", """{ "rootDir": "/strings" }""");

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.MalformedLocaleConfig, diagnostic.Code),
            () => Assert.Equal(config, diagnostic.Path),
            () => Assert.Equal(1, diagnostic.Line),
            () => Assert.Contains("not a relative folder", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_leave_a_locale_config_the_runtime_reads_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locale.json", """{ "rootNamespace": "Contoso.Widgets", "paths": ["$RootDir/$AnchorName.$Culture$Extension"] }""");

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(result.Diagnostics),
            () => Assert.Equal(1, result.FileCount));
    }

    [Fact]
    public async Task Should_report_a_locale_config_beneath_the_projects_root()
    {
        // Arrange — the runtime reads a config embedded anywhere as the assembly's, so the one place it may sit is stated at lint time.
        using var directory = new TempDirectory();
        var project = directory.Write("Scratch.csproj", """<Project Sdk="Microsoft.NET.Sdk" />""");
        var config = directory.Write(Path.Combine("Components", ".locale.json"), "{}");
        var resourceNames = new Dictionary<string, string> { [Path.GetFullPath(config)] = "Scratch/Components/.locale.json" };
        var compilation = CompilationTestFactory.Unreferenced("namespace Scratch.Components; public sealed class Widget;");
        var loader = new StubProjectLoader(new LoadedProject(project, compilation, false, false, EmbeddedResourceNames: resourceNames));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, projectLoader: loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.MalformedLocaleConfig, diagnostic.Code),
            () => Assert.Equal(config, diagnostic.Path),
            () => Assert.Contains("'Components'", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains("Scratch.csproj", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_report_a_locale_config_in_a_hosts_folder()
    {
        // Arrange — the host's folder is copied beside the application rather than embedded, and the runtime reads no config from it.
        using var directory = new TempDirectory();
        var project = directory.Write("Scratch.csproj", """<Project Sdk="Microsoft.NET.Sdk" />""");
        var config = directory.Write(Path.Combine(".locales", "Contoso.Widgets", ".locale.json"), """{ "rootNamespace": "Contoso" }""");
        var compilation = CompilationTestFactory.Unreferenced("namespace Scratch; public sealed class Widget;");
        var loader = new StubProjectLoader(new LoadedProject(project, compilation, false, false, EmbeddedResourceNames: new Dictionary<string, string>()));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, projectLoader: loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.MalformedLocaleConfig, diagnostic.Code),
            () => Assert.Equal(config, diagnostic.Path),
            () => Assert.Contains("host", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_report_a_locale_config_inside_a_locale_pack()
    {
        // Arrange — a pack's build embeds every JSON file beneath it, and the runtime reads no config from a pack, so the file is embedded and ignored.
        using var directory = new TempDirectory();
        var pack = directory.Write(Path.Combine("Packs", "Contoso.Widgets.Locales.Hebrew.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        var config = directory.Write(Path.Combine("Packs", ".locale.json"), """{ "rootNamespace": "Contoso" }""");
        var resourceNames = new Dictionary<string, string> { [Path.GetFullPath(config)] = ".locale.json" };
        var loader = new StubProjectLoader(new LoadedProject(pack, CompilationTestFactory.Building("Contoso.Widgets.Locales.Hebrew"), false, false, EmbeddedResourceNames: resourceNames, IsLocalePack: true));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, projectLoader: loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.MalformedLocaleConfig, diagnostic.Code),
            () => Assert.Equal(config, diagnostic.Path),
            () => Assert.Contains("A locale pack carries no config", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_report_a_locale_config_at_the_projects_root_that_the_project_does_not_embed()
    {
        // Arrange — the file sits where it belongs, but a project that embeds only some folders leaves it out of the assembly.
        using var directory = new TempDirectory();
        var project = directory.Write("Scratch.csproj", """<Project Sdk="Microsoft.NET.Sdk" />""");
        var config = directory.Write(".locale.json", "{}");
        var compilation = CompilationTestFactory.Unreferenced("namespace Scratch; public sealed class Widget;");
        var loader = new StubProjectLoader(new LoadedProject(project, compilation, false, false, EmbeddedResourceNames: new Dictionary<string, string>()));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, projectLoader: loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(config, diagnostic.Path),
            () => Assert.Contains("does not embed", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains("never reads it", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_report_an_entry_two_embedded_files_give_different_texts()
    {
        // Arrange — the config puts a second form in force beside the default, and one folder's file is written in both, worded apart.
        using var directory = new TempDirectory();
        var project = directory.Write("Scratch.csproj", """<Project Sdk="Microsoft.NET.Sdk" />""");
        var config = directory.Write(".locale.json", """{ "paths": ["$RootDir/$AnchorName/$LocalesDir/$Culture$Extension", "$RootDir/$AnchorName.$Culture$Extension"] }""");
        var first = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var second = directory.Write("Components.he.json", LocalizationFileFakeData.WithSection("Widget", "Today", "עכשיו"));
        var resourceNames = new Dictionary<string, string>
        {
            [Path.GetFullPath(config)] = "Scratch/.locale.json",
            [Path.GetFullPath(first)] = "Scratch/Components/.locales/he.json",
            [Path.GetFullPath(second)] = "Scratch/Components.he.json",
        };
        var compilation = CompilationTestFactory.Unreferenced("namespace Scratch.Components; public sealed class Widget;");
        var loader = new StubProjectLoader(new LoadedProject(project, compilation, false, false, EmbeddedResourceNames: resourceNames));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, LintingSeverity.Warning, loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert — the runtime keeps the text of the file whose name sorts last, so the other is the one set aside.
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.EntryConflict, diagnostic.Code),
            () => Assert.Equal(second, diagnostic.Path),
            () => Assert.Contains("'Today' different texts for 'Scratch.Components.Widget' in 'he'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_report_a_pack_file_whose_name_starts_with_no_assembly_of_the_solution()
    {
        // Arrange — the solution lists every project, so a name starting with none of them and nothing they reference names an assembly built nowhere.
        using var directory = new TempDirectory();
        var solution = directory.Write("Scratch.slnx", "<Solution />");
        var pack = directory.Write(Path.Combine("Packs", "Contoso.Widgets.Locales.Hebrew.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        var file = directory.Write(Path.Combine("Packs", "Contoso.Widgetz", "he.json"), LocalizationFileFakeData.Valid);
        var resourceNames = new Dictionary<string, string> { [Path.GetFullPath(file)] = "Contoso.Widgetz/he.json" };
        var loader = new StubProjectLoader(
            new LoadedProject(pack, CompilationTestFactory.Building("Contoso.Widgets.Locales.Hebrew"), false, false, EmbeddedResourceNames: resourceNames, IsLocalePack: true),
            new LoadedProject(Path.Combine(directory.Root, "Widgets", "Contoso.Widgets.csproj"), CompilationTestFactory.Building("Contoso.Widgets", "namespace Contoso.Widgets; public sealed class Widget;"), false, false));

        // Act
        var result = await LintingServiceTestFactory.Create(solution, projectLoader: loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.UnknownLocalePackAssembly, diagnostic.Code),
            () => Assert.Equal(file, diagnostic.Path),
            () => Assert.Contains("'Contoso.Widgetz'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_read_a_pack_for_the_project_its_files_name()
    {
        // Arrange — the pack's file is held to the target's types, and reported where it sits.
        using var directory = new TempDirectory();
        var pack = directory.Write(Path.Combine("Packs", "Contoso.Widgets.Locales.Hebrew.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        var file = directory.Write(Path.Combine("Packs", "Contoso.Widgets", "he.json"), LocalizationFileFakeData.WithSections(("Widget", "Today", "היום"), ("Gadget", "Today", "היום")));
        var resourceNames = new Dictionary<string, string> { [Path.GetFullPath(file)] = "Contoso.Widgets/he.json" };
        var loader = new StubProjectLoader(
            new LoadedProject(pack, CompilationTestFactory.Building("Contoso.Widgets.Locales.Hebrew"), false, false, EmbeddedResourceNames: resourceNames, IsLocalePack: true),
            new LoadedProject(Path.Combine(directory.Root, "Widgets", "Contoso.Widgets.csproj"), CompilationTestFactory.Building("Contoso.Widgets", "namespace Contoso.Widgets; public sealed class Widget;"), false, false, EmbeddedResourceNames: new Dictionary<string, string>()));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, projectLoader: loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.UnreachableLocalization, diagnostic.Code),
            () => Assert.Equal(file, diagnostic.Path),
            () => Assert.Contains("'Gadget'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_leave_a_pack_folder_alone_when_the_run_named_a_directory()
    {
        // Arrange — a directory lists no project but what sits beneath it, so the assembly may be built elsewhere.
        using var directory = new TempDirectory();
        var pack = directory.Write(Path.Combine("Packs", "Contoso.Widgets.Locales.Hebrew.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        var file = directory.Write(Path.Combine("Packs", "Contoso.Widgetz", "he.json"), LocalizationFileFakeData.Valid);
        var resourceNames = new Dictionary<string, string> { [Path.GetFullPath(file)] = "Contoso.Widgetz/he.json" };
        var loader = new StubProjectLoader(new LoadedProject(pack, CompilationTestFactory.Building("Contoso.Widgets.Locales.Hebrew"), false, false, EmbeddedResourceNames: resourceNames, IsLocalePack: true));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, projectLoader: loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task Should_report_a_key_left_out_for_the_type_a_property_another_project_of_the_run_builds_names()
    {
        // Arrange — the project reads the other's reference assembly, and loads first, as a load of a built tree may hand it over.
        using var directory = new TempDirectory();
        var componentsProject = directory.Write(Path.Combine("Components", "Contoso.Components.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        var project = directory.Write(Path.Combine("Widgets", "Scratch.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        var source = directory.Write(Path.Combine("Widgets", "Widget.cs"), """
            namespace Scratch;

            public sealed class Widget : Contoso.Components.Component
            {
                public void Show() => _ = L["Tomorrow"];
            }
            """);
        directory.Write(Path.Combine("Widgets", ".locales", "Widget.he.json"), LocalizationFileFakeData.Valid);
        directory.Write(".locali.json", """
            {
              "hints": {
                "localeMembers": [
                  { "assembly": "Contoso.Components", "type": "Contoso.Components.Component", "member": "L", "typeMember": "LocalizationSourceType" }
                ]
              }
            }
            """);
        var components = CompilationTestFactory.Referenced("""
            using System;

            using Forma.Framework.Localization;

            namespace Contoso.Components;

            public abstract class Component
            {
                protected virtual Type LocalizationSourceType => GetType();

                protected IStringLocale L => default!;
            }
            """).WithAssemblyName("Contoso.Components");
        var compilation = CompilationTestFactory.ReferencedFiles("Scratch", source).AddReferences(CompilationTestFactory.MetadataOf(components));
        var loader = new StubProjectLoader(new LoadedProject(project, compilation, false, false), new LoadedProject(componentsProject, components, false, false));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, LintingSeverity.Warning, loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.MissingLocalization, diagnostic.Code),
            () => Assert.Equal(source, diagnostic.Path),
            () => Assert.Contains("'Tomorrow' has no entry", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains("'Scratch.Widget'", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains($"by the hint at '{Path.Combine("..", ".locali.json")}:4'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_read_the_hints_a_project_of_the_run_carries_for_its_own_members()
    {
        // Arrange — the library states its member's relationship beside its project, and the lookup in another project reads it.
        using var directory = new TempDirectory();
        var componentsProject = directory.Write(Path.Combine("Components", "Contoso.Components.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        directory.Write(Path.Combine("Components", ".locali-hints.json"), """
            {
              "localeMembers": [
                { "assembly": "Contoso.Components", "type": "Contoso.Components.Component", "member": "L", "typeMember": "LocalizationSourceType" }
              ]
            }
            """);
        var project = directory.Write(Path.Combine("Widgets", "Scratch.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        var source = directory.Write(Path.Combine("Widgets", "Widget.cs"), """
            namespace Scratch;

            public sealed class Widget : Contoso.Components.Component
            {
                public void Show() => _ = L["Tomorrow"];
            }
            """);
        directory.Write(Path.Combine("Widgets", ".locales", "Widget.he.json"), LocalizationFileFakeData.Valid);
        var components = CompilationTestFactory.Referenced("""
            using System;

            using Forma.Framework.Localization;

            namespace Contoso.Components;

            public abstract class Component
            {
                protected virtual Type LocalizationSourceType => GetType();

                protected IStringLocale L => default!;
            }
            """).WithAssemblyName("Contoso.Components");
        var compilation = CompilationTestFactory.ReferencedFiles("Scratch", source).AddReferences(CompilationTestFactory.MetadataOf(components));
        var loader = new StubProjectLoader(new LoadedProject(project, compilation, false, false), new LoadedProject(componentsProject, components, false, false));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, LintingSeverity.Warning, loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.MissingLocalization, diagnostic.Code),
            () => Assert.Contains("'Scratch.Widget'", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains($"by the hint at '{Path.Combine("..", "Components", ".locali-hints.json")}:3'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_report_a_hint_naming_a_type_its_assembly_does_not_declare_where_it_is_written()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (widgets, widgetsProject) = WidgetsProject(directory, LocalizationFileFakeData.Valid);
        var configuration = directory.Write(".locali.json", """
            {
              "hints": {
                "localeMembers": [
                  { "assembly": "Contoso.Widgets", "type": "Contoso.Widgets.Missing", "member": "L", "typeMember": "LocaleSourceType" }
                ]
              }
            }
            """);
        var loader = new StubProjectLoader(widgetsProject);

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, LintingSeverity.Warning, loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.InvalidHint, diagnostic.Code),
            () => Assert.Equal(configuration, diagnostic.Path),
            () => Assert.Equal(4, diagnostic.Line));
    }

    [Fact]
    public async Task Should_count_how_far_the_run_checked_its_lookups()
    {
        // Arrange — one lookup reads its type's own files, and another a locale a factory the code builds makes.
        using var directory = new TempDirectory();
        var project = directory.Write(Path.Combine("Widgets", "Scratch.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        var source = directory.Write(Path.Combine("Widgets", "Widget.cs"), """
            using System;

            using Forma.Framework.Localization;

            namespace Scratch;

            internal sealed class Factory : IStringLocaleFactory
            {
                public IStringLocale Create(Type resourceSource) => throw new NotSupportedException();

                public IStringLocale Create(string baseName, string location) => throw new NotSupportedException();
            }

            public sealed class Widget(IStringLocale<Widget> locale)
            {
                public string Show() => locale["Today"].Value;

                public string Other() => new Factory().Create(typeof(Widget))["Tomorrow"].Value;
            }
            """);
        directory.Write(Path.Combine("Widgets", ".locales", "Widget.he.json"), LocalizationFileFakeData.Valid);
        var loader = new StubProjectLoader(new LoadedProject(project, CompilationTestFactory.ReferencedFiles("Scratch", source), false, false));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, LintingSeverity.Warning, loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var coverage = result.Coverage;
        Assert.Multiple(
            () => Assert.Equal((2, 1, 0, 1), (coverage.Lookups, coverage.FullyChecked, coverage.PartiallyChecked, coverage.Unchecked)),
            () => Assert.Equal(new LookupEvidence(1, 0, 0), coverage.Evidence),
            () => Assert.Equal(new Dictionary<string, int> { ["customFactory"] = 1 }, coverage.Reasons));
    }

    [Fact]
    public async Task Should_list_each_lookup_the_run_could_not_check_with_what_kept_it_unchecked()
    {
        // Arrange — a factory the code builds makes the locale, so no path reaches the type it is read for.
        using var directory = new TempDirectory();
        var project = directory.Write(Path.Combine("Widgets", "Scratch.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        var source = directory.Write(Path.Combine("Widgets", "Widget.cs"), """
            using System;

            using Forma.Framework.Localization;

            namespace Scratch;

            internal sealed class Factory : IStringLocaleFactory
            {
                public IStringLocale Create(Type resourceSource) => throw new NotSupportedException();

                public IStringLocale Create(string baseName, string location) => throw new NotSupportedException();
            }

            public sealed class Widget
            {
                public string Show() => new Factory().Create(typeof(Widget))["Tomorrow"].Value;
            }
            """);
        directory.Write(Path.Combine("Widgets", ".locales", "Widget.he.json"), LocalizationFileFakeData.Valid);
        var loader = new StubProjectLoader(new LoadedProject(project, CompilationTestFactory.ReferencedFiles("Scratch", source), false, false));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, LintingSeverity.Warning, loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var entry = Assert.Single(result.Coverage.UncheckedLookups);
        Assert.Multiple(
            () => Assert.Empty(result.Diagnostics),
            () => Assert.Equal((source, 16), (entry.Path, entry.Line)),
            () => Assert.Equal(["customFactory"], entry.Reasons),
            () => Assert.Contains("a factory other than the host's", entry.Explanation, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_count_and_list_a_key_written_as_an_expression_as_unchecked()
    {
        // Arrange
        using var directory = new TempDirectory();
        var project = directory.Write(Path.Combine("Widgets", "Scratch.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        var source = directory.Write(Path.Combine("Widgets", "Widget.cs"), """
            using Forma.Framework.Localization;

            namespace Scratch;

            public sealed class Widget(IStringLocale<Widget> locale)
            {
                public string Show(object column) => locale[column.ToString()!].Value;
            }
            """);
        directory.Write(Path.Combine("Widgets", ".locales", "Widget.he.json"), LocalizationFileFakeData.Valid);
        var loader = new StubProjectLoader(new LoadedProject(project, CompilationTestFactory.ReferencedFiles("Scratch", source), false, false));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, LintingSeverity.Warning, loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var coverage = result.Coverage;
        var entry = Assert.Single(coverage.UncheckedLookups);
        Assert.Multiple(
            () => Assert.Empty(result.Diagnostics),
            () => Assert.Equal((1, 0, 1), (coverage.Lookups, coverage.FullyChecked, coverage.Unchecked)),
            () => Assert.Equal(new Dictionary<string, int> { ["keyNotWritten"] = 1 }, coverage.Reasons),
            () => Assert.Equal(7, entry.Line),
            () => Assert.Equal(["keyNotWritten"], entry.Reasons));
    }

    [Fact]
    public async Task Should_tell_how_a_hint_lets_consumers_check_a_public_library_methods_key()
    {
        // Arrange — the library's own run makes no call writing the key, which its consumers write outside the run.
        using var directory = new TempDirectory();
        var project = directory.Write(Path.Combine("Widgets", "Scratch.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        var source = directory.Write(Path.Combine("Widgets", "Widget.cs"), """
            using Forma.Framework.Localization;

            namespace Scratch;

            public sealed class Widget(IStringLocale<Widget> locale)
            {
                public string Label(string key) => locale[key].Value;
            }
            """);
        directory.Write(Path.Combine("Widgets", ".locales", "Widget.he.json"), LocalizationFileFakeData.Valid);
        var loader = new StubProjectLoader(new LoadedProject(project, CompilationTestFactory.ReferencedFiles("Scratch", source), false, false));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, LintingSeverity.Warning, loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var entry = Assert.Single(result.Coverage.UncheckedLookups);
        Assert.Multiple(
            () => Assert.Empty(result.Diagnostics),
            () => Assert.Equal(["keyFromParameter"], entry.Reasons),
            () => Assert.Contains("a localeKeys hint its body bears out lets its consumers' runs check the calls they make", entry.Explanation, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_check_a_key_a_lookup_takes_from_a_parameter_at_the_calls_writing_it()
    {
        // Arrange — one call writes a key the file leaves out, and another writes its key as an expression.
        using var directory = new TempDirectory();
        var project = directory.Write(Path.Combine("Widgets", "Scratch.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        var source = directory.Write(Path.Combine("Widgets", "Widget.cs"), """
            using Forma.Framework.Localization;

            namespace Scratch;

            public sealed class Widget(IStringLocale<Widget> locale)
            {
                private string Label(string key) => locale[key].Value;

                public string Show(object column) => Label("Tomorrow") + Label(column.ToString()!);
            }
            """);
        directory.Write(Path.Combine("Widgets", ".locales", "Widget.he.json"), LocalizationFileFakeData.Valid);
        var loader = new StubProjectLoader(new LoadedProject(project, CompilationTestFactory.ReferencedFiles("Scratch", source), false, false));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, LintingSeverity.Warning, loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        var unread = Assert.Single(result.Coverage.UncheckedLookups);
        Assert.Multiple(
            () => Assert.Equal((LintingCodes.MissingLocalization, 9), (diagnostic.Code, diagnostic.Line)),
            () => Assert.StartsWith("'Tomorrow' has no entry", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Equal(7, unread.Line),
            () => Assert.Equal(["keyFromParameter"], unread.Reasons),
            () => Assert.Contains("the parameter 'key'", unread.Explanation, StringComparison.Ordinal),
            () => Assert.DoesNotContain("localeKeys", unread.Explanation, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_report_a_key_left_out_of_the_files_of_the_assembly_declaring_the_type()
    {
        // Arrange — the lookup sits in one project and reads a type the other declares, whose files the runtime reads.
        using var directory = new TempDirectory();
        var (widgets, widgetsProject) = WidgetsProject(directory, LocalizationFileFakeData.Valid);
        var appProject = directory.Write(Path.Combine("App", "Contoso.App.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        var source = directory.Write(Path.Combine("App", "Page.cs"), """
            using Forma.Framework.Localization;

            namespace Contoso.App;

            public sealed class Page
            {
                private IStringLocale<Contoso.Widgets.Widget> L { get; } = default!;

                public void Show() => _ = L["Tomorrow"];
            }
            """);
        var app = CompilationTestFactory.ReferencedFiles("Contoso.App", source).AddReferences(CompilationTestFactory.MetadataOf(widgets));
        var loader = new StubProjectLoader(new LoadedProject(appProject, app, false, false), widgetsProject);

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, LintingSeverity.Warning, loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.MissingLocalization, diagnostic.Code),
            () => Assert.Equal(source, diagnostic.Path),
            () => Assert.Contains("'Contoso.Widgets.Widget' in 'he'", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_read_a_type_in_its_own_assembly_rather_than_one_declaring_the_same_name()
    {
        // Arrange — the app declares a type of the same full name whose file answers the key; the lookup reads the other assembly's.
        using var directory = new TempDirectory();
        var (widgets, widgetsProject) = WidgetsProject(directory, LocalizationFileFakeData.Valid);
        var appProject = directory.Write(Path.Combine("App", "Contoso.App.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        directory.Write(Path.Combine("App", ".locales", "Contoso.Widgets.Widget.he.json"), LocalizationFileFakeData.WithSection("he", "Contoso.Widgets.Widget", "Tomorrow", "מחר"));
        var source = directory.Write(Path.Combine("App", "Page.cs"), """
            extern alias widgets;

            using Forma.Framework.Localization;

            namespace Contoso.Widgets
            {
                public sealed class Widget;
            }

            namespace Contoso.App
            {
                public sealed class Page
                {
                    private IStringLocale<widgets::Contoso.Widgets.Widget> L { get; } = default!;

                    public void Show() => _ = L["Tomorrow"];
                }
            }
            """);
        var app = CompilationTestFactory.ReferencedFiles("Contoso.App", source).AddReferences(CompilationTestFactory.MetadataOf(widgets).WithAliases(["widgets"]));
        var loader = new StubProjectLoader(new LoadedProject(appProject, app, false, false), widgetsProject);

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, LintingSeverity.Warning, loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == LintingCodes.MissingLocalization && diagnostic.Path == source);
    }

    [Fact]
    public async Task Should_leave_a_key_of_a_type_no_project_of_the_run_builds_alone()
    {
        // Arrange — the assembly declaring the type is read as metadata alone, so none of its entries can be read.
        using var directory = new TempDirectory();
        var (widgets, _) = WidgetsProject(directory, LocalizationFileFakeData.Valid);
        var appProject = directory.Write(Path.Combine("App", "Contoso.App.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        directory.Write(Path.Combine("App", ".locales", "Page.he.json"), LocalizationFileFakeData.Valid);
        var source = directory.Write(Path.Combine("App", "Page.cs"), """
            using Forma.Framework.Localization;

            namespace Contoso.App;

            public sealed class Page
            {
                private IStringLocale<Contoso.Widgets.Widget> L { get; } = default!;

                public void Show() => _ = L["Tomorrow"];
            }
            """);
        var app = CompilationTestFactory.ReferencedFiles("Contoso.App", source).AddReferences(CompilationTestFactory.MetadataOf(widgets));
        var loader = new StubProjectLoader(new LoadedProject(appProject, app, false, false));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, LintingSeverity.Warning, loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == LintingCodes.MissingLocalization);
    }

    [Fact]
    public async Task Should_leave_a_key_an_applications_file_for_the_declaring_assembly_answers_alone()
    {
        // Arrange — the runtime lays an application's files for another assembly over that assembly's own.
        using var directory = new TempDirectory();
        var (widgets, widgetsProject) = WidgetsProject(directory, LocalizationFileFakeData.Valid);
        var appProject = directory.Write(Path.Combine("App", "Contoso.App.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        directory.Write(Path.Combine("App", ".locales", "he", "Contoso.Widgets.json"), LocalizationFileFakeData.WithSection("he", "Widget", "Tomorrow", "מחר"));
        var source = directory.Write(Path.Combine("App", "Program.cs"), """
            using Forma.Framework.Localization;

            namespace Contoso.App;

            public static class Program
            {
                private static IStringLocale<Contoso.Widgets.Widget> L { get; } = default!;

                public static void Main() => _ = L["Tomorrow"];
            }
            """);
        var app = CompilationTestFactory.ReferencedFiles("Contoso.App", source).AddReferences(CompilationTestFactory.MetadataOf(widgets));
        app = app.WithOptions(app.Options.WithOutputKind(OutputKind.ConsoleApplication));
        var loader = new StubProjectLoader(new LoadedProject(appProject, app, false, false), widgetsProject);

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, LintingSeverity.Warning, loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == LintingCodes.MissingLocalization);
    }

    [Fact]
    public async Task Should_report_a_type_an_applications_file_localizes_in_each_culture_of_its_assembly_the_file_leaves_out()
    {
        // Arrange — the library localizes only Gadget, in Hebrew and Spanish, and the application gives Widget Hebrew alone.
        using var directory = new TempDirectory();
        var widgetsProject = directory.Write(Path.Combine("Widgets", "Contoso.Widgets.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        directory.Write(Path.Combine("Widgets", ".locales", "Gadget.he.json"), LocalizationFileFakeData.Valid);
        directory.Write(Path.Combine("Widgets", ".locales", "Gadget.es.json"), LocalizationFileFakeData.ForCulture("es", "Today", "hoy"));
        var widgets = CompilationTestFactory.Referenced("namespace Contoso.Widgets; public sealed class Widget; public sealed class Gadget;").WithAssemblyName("Contoso.Widgets");
        var appProject = directory.Write(Path.Combine("App", "Contoso.App.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        directory.Write(Path.Combine("App", ".locales", "he", "Contoso.Widgets.json"), LocalizationFileFakeData.WithSection("he", "Widget", "Tomorrow", "מחר"));
        var source = directory.Write(Path.Combine("App", "Program.cs"), """
            using Forma.Framework.Localization;

            namespace Contoso.App;

            public static class Program
            {
                private static IStringLocale<Contoso.Widgets.Widget> L { get; } = default!;

                public static void Main() => _ = L["Tomorrow"];
            }
            """);
        var app = CompilationTestFactory.ReferencedFiles("Contoso.App", source).AddReferences(CompilationTestFactory.MetadataOf(widgets));
        app = app.WithOptions(app.Options.WithOutputKind(OutputKind.ConsoleApplication));
        var loader = new StubProjectLoader(new LoadedProject(appProject, app, false, false), new LoadedProject(widgetsProject, widgets, false, false));

        // Act
        var result = await LintingServiceTestFactory.Create(directory.Root, LintingSeverity.Warning, loader).RunAsync(TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == LintingCodes.UnlocalizedType);
        Assert.Contains("no entries in 'es'", diagnostic.Message, StringComparison.Ordinal);
    }

    // A library declaring Contoso.Widgets.Widget, with a Hebrew file for it, written under its own folder of the run.
    private static (Compilation Compilation, LoadedProject Project) WidgetsProject(TempDirectory directory, string hebrew)
    {
        var path = directory.Write(Path.Combine("Widgets", "Contoso.Widgets.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        directory.Write(Path.Combine("Widgets", ".locales", "Widget.he.json"), hebrew);
        var compilation = CompilationTestFactory.Referenced("namespace Contoso.Widgets; public sealed class Widget;").WithAssemblyName("Contoso.Widgets");

        return (compilation, new LoadedProject(path, compilation, false, false));
    }
}
