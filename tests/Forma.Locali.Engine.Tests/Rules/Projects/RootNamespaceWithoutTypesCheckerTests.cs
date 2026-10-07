namespace Forma.Locali.Tests.Rules.Projects;

using Forma.Locali.Projects;
using Forma.Locali.Rules.Projects;

public sealed class RootNamespaceWithoutTypesCheckerTests
{
    private const string Widget = """
        namespace Contoso.Widgets.Bars;

        public sealed class Widget;
        """;

    [Fact]
    public void Should_report_a_declared_root_namespace_no_type_is_under()
    {
        // Arrange — the project builds under one root namespace and its config declares another, as a rename of one leaves them.
        using var directory = new TempDirectory();
        var config = directory.Write(".locale.json", """{ "rootNamespace": "Contoso.Gadgets" }""");

        // Act
        var diagnostic = Assert.Single(Check(directory, Widget, "Contoso.Widgets", config));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.RootNamespaceWithoutTypes, diagnostic.Code),
            () => Assert.Equal(config, diagnostic.Path),
            () => Assert.Contains("'Contoso.Gadgets'", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains("'Contoso.Widgets', its RootNamespace", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_report_an_assembly_name_no_type_is_under_where_no_root_namespace_is_declared()
    {
        // Arrange — the assembly is Scratch, its types sit under the project's own root namespace, and nothing tells the runtime so.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Bars", ".locales", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));

        // Act
        var diagnostic = Assert.Single(Check(directory, Widget, "Contoso.Widgets", null, file));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.RootNamespaceWithoutTypes, diagnostic.Code),
            () => Assert.Equal(Path.Combine(directory.Root, "Scratch.csproj"), diagnostic.Path),
            () => Assert.Contains("'Scratch', the assembly's name", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains("'Contoso.Widgets', its RootNamespace", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_report_an_assembly_name_no_type_is_under_where_a_pack_alone_localizes_the_project()
    {
        // Arrange — the project embeds no file of its own, and the pack lays its folder out by the assembly's name, under which no type sits.
        using var directory = new TempDirectory();
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced(Widget), false, false, "Contoso.Widgets", new Dictionary<string, string>());
        var packFile = directory.Write(Path.Combine(LocalePackTestFactory.Folder, "Scratch", "Bars", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var pack = LocalePackTestFactory.Resolve(directory, project, packFile);

        // Act
        var diagnostic = Assert.Single(ProjectLinterTestFactory.Check(new RootNamespaceWithoutTypesChecker(), project, [], packs: [pack]));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.RootNamespaceWithoutTypes, diagnostic.Code),
            () => Assert.Equal(project.Path, diagnostic.Path),
            () => Assert.Contains("'Scratch', the assembly's name", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains("a host or a pack", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_leave_a_pack_laid_out_by_full_names_alone()
    {
        // Arrange — a pack may spell a type's full name beneath the assembly's folder, which the runtime reads for a type outside the root, so nothing is stranded.
        using var directory = new TempDirectory();
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced(Widget), false, false, "Contoso.Widgets", new Dictionary<string, string>());
        var packFile = directory.Write(Path.Combine(LocalePackTestFactory.Folder, "Scratch", "Contoso.Widgets.Bars", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var pack = LocalePackTestFactory.Resolve(directory, project, packFile);

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new RootNamespaceWithoutTypesChecker(), project, [], packs: [pack]);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_declared_root_namespace_the_types_are_under_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var config = directory.Write(".locale.json", """{ "rootNamespace": "Contoso.Widgets" }""");

        // Act
        var diagnostics = Check(directory, Widget, "Contoso.Widgets", config);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_declared_root_namespace_apart_from_the_projects_alone_when_types_are_under_it()
    {
        // Arrange — the runtime never reads the project's RootNamespace, so the two may differ where the types follow the config's.
        using var directory = new TempDirectory();
        var config = directory.Write(".locale.json", """{ "rootNamespace": "Contoso.Widgets" }""");

        // Act
        var diagnostics = Check(directory, Widget, "Scratch", config);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_project_with_no_locale_files_and_no_declared_root_namespace_alone()
    {
        // Arrange — nothing is localized, so nothing is laid out by any root.
        using var directory = new TempDirectory();

        // Act
        var diagnostics = Check(directory, Widget, "Contoso.Widgets", null);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_type_outside_any_namespace_to_the_root()
    {
        // Arrange — the runtime reads a type outside any namespace as the root's own.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("GlobalRoot", ".locales", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var diagnostics = Check(directory, "public sealed class GlobalRoot;", null, null, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_config_that_will_not_read_to_its_own_rule()
    {
        // Arrange
        using var directory = new TempDirectory();
        var config = directory.Write(".locale.json", """{ "rootNamespace": "Contoso..Gadgets" }""");
        var file = directory.Write(Path.Combine("Bars", ".locales", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));

        // Act
        var diagnostics = Check(directory, Widget, "Contoso.Widgets", config, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_stay_silent_when_the_projects_items_were_not_read()
    {
        // Arrange — without the items no config file is known to be embedded, so what the runtime counts from cannot be told.
        using var directory = new TempDirectory();
        var config = directory.Write(".locale.json", """{ "rootNamespace": "Contoso.Widgets" }""");
        var file = directory.Write(Path.Combine("Bars", ".locales", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced(Widget), false, false, "Contoso.Widgets");

        // Act
        var diagnostics = ProjectLinterTestFactory.Check(new RootNamespaceWithoutTypesChecker(), project, [file, config]);

        // Assert
        Assert.Empty(diagnostics);
    }

    private static IReadOnlyList<LintingDiagnostic> Check(TempDirectory directory, string source, string? rootNamespace, string? config, params string[] files)
    {
        var rootName = rootNamespace ?? "Scratch";
        var resourceNames = files.ToDictionary(
            Path.GetFullPath,
            file => string.Join('.', [rootName, .. Path.GetRelativePath(directory.Root, file).Split(Path.DirectorySeparatorChar)]));

        if (config is not null)
        {
            resourceNames[Path.GetFullPath(config)] = $"{rootName}..locale.json";
        }

        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced(source), false, false, rootNamespace, resourceNames);

        return ProjectLinterTestFactory.Check(new RootNamespaceWithoutTypesChecker(), project, [.. files, .. config is null ? Array.Empty<string>() : [config]]);
    }
}
