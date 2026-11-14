namespace Forma.Locali.Tests.Sources;

using Forma.Locali.Projects;

using Microsoft.CodeAnalysis;

public sealed class LocalePackFilesTests
{
    private const string Widget = "namespace Contoso.Widgets; public sealed class Widget;";

    [Fact]
    public void Should_resolve_a_file_to_the_project_building_the_assembly_its_name_starts_with()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var pack = Pack(directory, file);
        var target = Project(directory, CompilationTestFactory.Building("Contoso.Widgets", Widget));

        // Act
        var resolved = LocalePackFiles.Resolve(LocaleFormatTestFactory.Json.RuntimeFormat, pack, [file], new LoadedProjects([pack, target], IsWholeSolution: true))!;

        // Assert
        var assembly = Assert.Single(resolved.Assemblies);
        Assert.Multiple(
            () => Assert.Equal("Contoso.Widgets", assembly.AssemblyName),
            () => Assert.Same(target, assembly.Target),
            () => Assert.Equal("Contoso.Widgets.Bars.he.json", Assert.Single(assembly.Files).Name),
            () => Assert.True(resolved.IsEveryAssemblyKnown));
    }

    [Fact]
    public void Should_resolve_a_file_whose_culture_stands_before_the_assemblys_name()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("he", "Contoso.Widgets", "Bars.json"), LocalizationFileFakeData.Valid);
        var pack = Pack(directory, file);
        var target = Project(directory, CompilationTestFactory.Building("Contoso.Widgets", Widget));

        // Act
        var assembly = Assert.Single(LocalePackFiles.Resolve(LocaleFormatTestFactory.Json.RuntimeFormat, pack, [file], new LoadedProjects([pack, target], IsWholeSolution: true))!.Assemblies);

        // Assert
        Assert.Multiple(
            () => Assert.Same(target, assembly.Target),
            () => Assert.Equal("he", Assert.Single(assembly.Files).Culture.Name));
    }

    [Fact]
    public void Should_resolve_a_file_to_the_longest_assembly_its_name_starts_with()
    {
        // Arrange — one assembly's name may start another's, and the runtime reads the file for both, the shorter as another assembly's namespace.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets.Bars", "he.json"), LocalizationFileFakeData.Valid);
        var pack = Pack(directory, file);
        var widgets = Project(directory, CompilationTestFactory.Building("Contoso.Widgets", Widget));
        var bars = new LoadedProject(Path.Combine(directory.Root, "Bars", "Bars.csproj"), CompilationTestFactory.Building("Contoso.Widgets.Bars"), false, false);

        // Act
        var resolved = LocalePackFiles.Resolve(LocaleFormatTestFactory.Json.RuntimeFormat, pack, [file], new LoadedProjects([pack, widgets, bars], IsWholeSolution: true))!;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["Contoso.Widgets.Bars"], resolved.Assemblies.Select(assembly => assembly.AssemblyName)),
            () => Assert.Equal(["Contoso.Widgets", "Contoso.Widgets.Bars"], resolved.FileNames[file].AssemblyNames));
    }

    [Fact]
    public void Should_resolve_a_file_naming_an_assembly_a_project_references_to_no_project()
    {
        // Arrange — the assembly arrives from a package, so no project builds it and one references it.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var pack = Pack(directory, file);
        var host = Project(directory, CompilationTestFactory.Building("Contoso.Host").AddReferences(CompilationTestFactory.Building("Contoso.Widgets", Widget).ToMetadataReference()));

        // Act
        var assembly = Assert.Single(LocalePackFiles.Resolve(LocaleFormatTestFactory.Json.RuntimeFormat, pack, [file], new LoadedProjects([pack, host], IsWholeSolution: true))!.Assemblies);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Contoso.Widgets", assembly.AssemblyName),
            () => Assert.Null(assembly.Target));
    }

    [Fact]
    public void Should_name_no_assembly_for_a_file_whose_name_starts_with_none_of_the_run()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgetz", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var pack = Pack(directory, file);
        var target = Project(directory, CompilationTestFactory.Building("Contoso.Widgets", Widget));

        // Act
        var resolved = LocalePackFiles.Resolve(LocaleFormatTestFactory.Json.RuntimeFormat, pack, [file], new LoadedProjects([pack, target], IsWholeSolution: true))!;

        // Assert
        Assert.Multiple(
            () => Assert.Empty(resolved.Assemblies),
            () => Assert.Empty(resolved.FileNames[file].AssemblyNames));
    }

    [Fact]
    public void Should_name_no_assembly_for_a_file_named_for_its_culture_alone()
    {
        // Arrange — a name of no part but the culture starts with no assembly's.
        using var directory = new TempDirectory();
        var file = directory.Write("he.json", LocalizationFileFakeData.Valid);
        var pack = Pack(directory, file);

        // Act
        var resolved = LocalePackFiles.Resolve(LocaleFormatTestFactory.Json.RuntimeFormat, pack, [file], new LoadedProjects([pack], IsWholeSolution: true))!;

        // Assert
        Assert.Empty(resolved.FileNames[file].AssemblyNames);
    }

    [Fact]
    public void Should_know_every_assembly_only_when_the_run_named_a_solution()
    {
        // Arrange — a directory lists no project but what sits beneath it.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var pack = Pack(directory, file);

        // Act
        var resolved = LocalePackFiles.Resolve(LocaleFormatTestFactory.Json.RuntimeFormat, pack, [file], new LoadedProjects([pack], IsWholeSolution: false))!;

        // Assert
        Assert.False(resolved.IsEveryAssemblyKnown);
    }

    [Fact]
    public void Should_not_know_every_assembly_when_a_project_of_the_solution_did_not_compile()
    {
        // Arrange — a project that did not compile keeps its assembly's name to itself.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var pack = Pack(directory, file);
        var broken = Project(directory, null);

        // Act
        var resolved = LocalePackFiles.Resolve(LocaleFormatTestFactory.Json.RuntimeFormat, pack, [file], new LoadedProjects([pack, broken], IsWholeSolution: true))!;

        // Assert
        Assert.False(resolved.IsEveryAssemblyKnown);
    }

    [Fact]
    public void Should_keep_the_assembly_a_file_that_will_not_read_names()
    {
        // Arrange — an unreadable file is the file rules' business, and its name still names an assembly.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Unmarked);
        var pack = Pack(directory, file);
        var target = Project(directory, CompilationTestFactory.Building("Contoso.Widgets", Widget));

        // Act
        var assembly = Assert.Single(LocalePackFiles.Resolve(LocaleFormatTestFactory.Json.RuntimeFormat, pack, [file], new LoadedProjects([pack, target], IsWholeSolution: true))!.Assemblies);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Contoso.Widgets", assembly.AssemblyName),
            () => Assert.Empty(assembly.Files));
    }

    [Fact]
    public void Should_skip_a_file_the_pack_does_not_embed()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var pack = new LoadedProject(Path.Combine(directory.Root, "Contoso.Widgets.Locales.Hebrew.csproj"), CompilationTestFactory.Building("Contoso.Widgets.Locales.Hebrew"), false, false, EmbeddedResourceNames: new Dictionary<string, string>(), IsLocalePack: true);

        // Act
        var resolved = LocalePackFiles.Resolve(LocaleFormatTestFactory.Json.RuntimeFormat, pack, [file], new LoadedProjects([pack], IsWholeSolution: true))!;

        // Assert
        Assert.Empty(resolved.FileNames);
    }

    [Fact]
    public void Should_resolve_nothing_for_a_project_that_is_not_a_pack()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced(Widget), false, false, EmbeddedResourceNames: Names(directory, file));

        // Act
        var resolved = LocalePackFiles.Resolve(LocaleFormatTestFactory.Json.RuntimeFormat, project, [file], new LoadedProjects([project], IsWholeSolution: true));

        // Assert
        Assert.Null(resolved);
    }

    [Fact]
    public void Should_resolve_nothing_when_the_packs_items_were_not_read()
    {
        // Arrange — without the items, no file is known to be embedded under the name the runtime reads it by.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var pack = new LoadedProject(Path.Combine(directory.Root, "Contoso.Widgets.Locales.Hebrew.csproj"), CompilationTestFactory.Building("Contoso.Widgets.Locales.Hebrew"), false, false, IsLocalePack: true);

        // Act
        var resolved = LocalePackFiles.Resolve(LocaleFormatTestFactory.Json.RuntimeFormat, pack, [file], new LoadedProjects([pack], IsWholeSolution: true));

        // Assert
        Assert.Null(resolved);
    }

    private static LoadedProject Pack(TempDirectory directory, params string[] files)
        => new(Path.Combine(directory.Root, "Contoso.Widgets.Locales.Hebrew.csproj"), CompilationTestFactory.Building("Contoso.Widgets.Locales.Hebrew"), false, false, EmbeddedResourceNames: Names(directory, files), IsLocalePack: true);

    private static LoadedProject Project(TempDirectory directory, Compilation? compilation)
        => new(Path.Combine(directory.Root, "Target", "Target.csproj"), compilation, false, false);

    // A pack's build embeds each file under its path with the folders joined by dots.
    private static Dictionary<string, string> Names(TempDirectory directory, params string[] files)
        => files.ToDictionary(
            Path.GetFullPath,
            file => Path.GetRelativePath(directory.Root, file).Replace(Path.DirectorySeparatorChar, '.').Replace(Path.AltDirectorySeparatorChar, '.'),
            StringComparer.OrdinalIgnoreCase);
}
