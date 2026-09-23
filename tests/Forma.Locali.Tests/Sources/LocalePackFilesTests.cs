namespace Forma.Locali.Tests.Sources;

using Forma.Locali.Projects;

using Microsoft.CodeAnalysis;

public sealed class LocalePackFilesTests
{
    private const string Widget = "namespace Contoso.Widgets; public sealed class Widget;";

    [Fact]
    public void Should_resolve_a_folder_to_the_project_building_the_assembly_it_names()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var pack = Pack(directory, file);
        var target = Project(directory, CompilationTestFactory.Building("Contoso.Widgets", Widget));

        // Act
        var resolved = LocalePackFiles.Resolve(pack, [file], new LoadedProjects([pack, target], IsWholeSolution: true))!;

        // Assert
        var folder = Assert.Single(resolved.Folders);
        Assert.Multiple(
            () => Assert.Equal("Contoso.Widgets", folder.AssemblyName),
            () => Assert.Same(target, folder.Target),
            () => Assert.Equal("Contoso.Widgets.Bars.he.json", Assert.Single(folder.Files).Name),
            () => Assert.True(resolved.IsEveryAssemblyKnown));
    }

    [Fact]
    public void Should_mark_a_folder_naming_an_assembly_a_project_references()
    {
        // Arrange — the assembly arrives from a package, so no project builds it and one references it.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var pack = Pack(directory, file);
        var host = Project(directory, CompilationTestFactory.Building("Contoso.Host").AddReferences(CompilationTestFactory.Building("Contoso.Widgets", Widget).ToMetadataReference()));

        // Act
        var folder = Assert.Single(LocalePackFiles.Resolve(pack, [file], new LoadedProjects([pack, host], IsWholeSolution: true))!.Folders);

        // Assert
        Assert.Multiple(
            () => Assert.Null(folder.Target),
            () => Assert.True(folder.IsReferenced));
    }

    [Fact]
    public void Should_leave_a_folder_naming_no_assembly_of_the_run_unresolved()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgetz", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var pack = Pack(directory, file);
        var target = Project(directory, CompilationTestFactory.Building("Contoso.Widgets", Widget));

        // Act
        var folder = Assert.Single(LocalePackFiles.Resolve(pack, [file], new LoadedProjects([pack, target], IsWholeSolution: true))!.Folders);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Contoso.Widgetz", folder.AssemblyName),
            () => Assert.Null(folder.Target),
            () => Assert.False(folder.IsReferenced));
    }

    [Fact]
    public void Should_know_every_assembly_only_when_the_run_named_a_solution()
    {
        // Arrange — a directory lists no project but what sits beneath it.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var pack = Pack(directory, file);

        // Act
        var resolved = LocalePackFiles.Resolve(pack, [file], new LoadedProjects([pack], IsWholeSolution: false))!;

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
        var resolved = LocalePackFiles.Resolve(pack, [file], new LoadedProjects([pack, broken], IsWholeSolution: true))!;

        // Assert
        Assert.False(resolved.IsEveryAssemblyKnown);
    }

    [Fact]
    public void Should_keep_a_folder_whose_file_will_not_read()
    {
        // Arrange — an unreadable file is the file rules' business, and its folder still names an assembly.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Unmarked);
        var pack = Pack(directory, file);

        // Act
        var folder = Assert.Single(LocalePackFiles.Resolve(pack, [file], new LoadedProjects([pack], IsWholeSolution: true))!.Folders);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Contoso.Widgets", folder.AssemblyName),
            () => Assert.Empty(folder.Files));
    }

    [Fact]
    public void Should_skip_a_file_above_every_assemblys_folder()
    {
        // Arrange — the runtime reads a pack's files beneath a folder named for an assembly, so a file at the root belongs to none.
        using var directory = new TempDirectory();
        var file = directory.Write("he.json", LocalizationFileFakeData.Valid);
        var pack = Pack(directory, file);

        // Act
        var resolved = LocalePackFiles.Resolve(pack, [file], new LoadedProjects([pack], IsWholeSolution: true))!;

        // Assert
        Assert.Empty(resolved.Folders);
    }

    [Fact]
    public void Should_skip_a_file_the_pack_does_not_embed()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var pack = new LoadedProject(Path.Combine(directory.Root, "Contoso.Widgets.Locales.Hebrew.csproj"), CompilationTestFactory.Building("Contoso.Widgets.Locales.Hebrew"), false, false, EmbeddedResourceNames: new Dictionary<string, string>(), IsLocalePack: true);

        // Act
        var resolved = LocalePackFiles.Resolve(pack, [file], new LoadedProjects([pack], IsWholeSolution: true))!;

        // Assert
        Assert.Empty(resolved.Folders);
    }

    [Fact]
    public void Should_resolve_nothing_for_a_project_that_is_not_a_pack()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced(Widget), false, false, EmbeddedResourceNames: Names(directory, file));

        // Act
        var resolved = LocalePackFiles.Resolve(project, [file], new LoadedProjects([project], IsWholeSolution: true));

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
        var resolved = LocalePackFiles.Resolve(pack, [file], new LoadedProjects([pack], IsWholeSolution: true));

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
