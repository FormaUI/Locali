namespace Forma.Locali.Tests.Sources;

using System.Globalization;

using Forma.Locali.Projects;

using Microsoft.CodeAnalysis;

public sealed class ProjectLocaleFilesTests
{
    [Fact]
    public void Should_name_a_file_for_its_folder_position_under_the_root()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", "Explorer", ".locales", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var resolved = Assert.Single(Resolve(directory, Compile("namespace Scratch;"), file).Files);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Scratch.Components.Explorer..locales.he.json", resolved.Name),
            () => Assert.Equal(CultureInfo.GetCultureInfo("he"), resolved.Culture));
    }

    [Fact]
    public void Should_name_a_file_under_the_root_namespace_rather_than_the_assembly_name()
    {
        // Arrange — an AddOn embeds under the namespace it borrows, not its own name.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Emojis", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), Compile("namespace Scratch;"), false, false, "Forma.Framework");

        // Act
        var resolved = Assert.Single(ProjectLocaleFiles.Resolve(project, [file])!.Files);

        // Assert
        Assert.Equal("Forma.Framework.Emojis..locales.he.json", resolved.Name);
    }

    [Theory]
    [InlineData("my-folder")]
    [InlineData("My Folder")]
    [InlineData("2024")]
    public void Should_skip_a_file_whose_folder_the_build_renames_when_the_projects_items_were_not_read(string folder)
    {
        // Arrange — the SDK embeds such a folder under an identifier only the build knows, so the name cannot be told without the project's items.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(folder, ".locales", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var resolved = Resolve(directory, Compile("namespace Scratch;"), file);

        // Assert
        Assert.Empty(resolved.Files);
    }

    [Fact]
    public void Should_read_a_file_by_the_name_the_build_gives_it()
    {
        // Arrange — a project may give a file a name of its own, which the build embeds it under.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Widgets", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var resourceNames = new Dictionary<string, string> { [Path.GetFullPath(file)] = "Contoso.Widgets..locales.he.json" };
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), Compile("namespace Scratch;"), false, false, EmbeddedResourceNames: resourceNames);

        // Act
        var resolved = Assert.Single(ProjectLocaleFiles.Resolve(project, [file])!.Files);

        // Assert
        Assert.Equal("Contoso.Widgets..locales.he.json", resolved.Name);
    }

    [Theory]
    [InlineData("es-419.json")]
    [InlineData("ResourceFormatter.es-419.json")]
    public void Should_carry_the_culture_the_file_name_gives_it(string name)
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(name, LocalizationFileFakeData.ForCulture("es-419", "Today", "hoy"));

        // Act
        var resolved = Assert.Single(Resolve(directory, Compile("namespace Scratch;"), file).Files);

        // Assert
        Assert.Equal(CultureInfo.GetCultureInfo("es-419"), resolved.Culture);
    }

    [Fact]
    public void Should_skip_a_file_carrying_no_recognised_culture()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write("launchSettings.json", LocalizationFileFakeData.Valid);

        // Act
        var resolved = Resolve(directory, Compile("namespace Scratch;"), file);

        // Assert
        Assert.Empty(resolved.Files);
    }

    [Fact]
    public void Should_skip_a_file_the_runtime_cannot_read()
    {
        // Arrange — an unreadable file is the file rules' business, and the runtime reads nothing from it.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "he.json"), LocalizationFileFakeData.Unmarked);

        // Act
        var resolved = Resolve(directory, Compile("namespace Scratch;"), file);

        // Assert
        Assert.Empty(resolved.Files);
    }

    [Fact]
    public void Should_skip_a_file_the_project_does_not_embed()
    {
        // Arrange — the host's own folder is copied beside the application and translates other assemblies' types.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "Forma.Blazor", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), Compile("namespace Scratch;"), false, false, EmbeddedResourceNames: new Dictionary<string, string>());

        // Act
        var resolved = ProjectLocaleFiles.Resolve(project, [file])!;

        // Assert
        Assert.Empty(resolved.Files);
    }

    [Fact]
    public void Should_list_every_type_the_project_declares()
    {
        // Arrange
        using var directory = new TempDirectory();
        var compilation = Compile("""
            namespace Scratch.Bars;

            public sealed class Outer
            {
                public sealed class Inner<TValue>;
            }
            """);

        // Act
        var resolved = Resolve(directory, compilation);

        // Assert
        Assert.Equal(["Scratch.Bars.Outer", "Scratch.Bars.Outer.Inner"], resolved.TypeNames);
    }

    [Fact]
    public void Should_resolve_nothing_when_the_project_failed_to_load()
    {
        // Arrange
        using var directory = new TempDirectory();
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), null, false, false);

        // Act
        var resolved = ProjectLocaleFiles.Resolve(project, [directory.Write(Path.Combine(".locales", "he.json"), LocalizationFileFakeData.Valid)]);

        // Assert
        Assert.Null(resolved);
    }

    [Fact]
    public void Should_read_a_pack_folder_naming_the_project_for_its_types()
    {
        // Arrange — the pack's folder for the assembly is laid out by namespace, and its sections name the types.
        using var directory = new TempDirectory();
        var target = Target(directory, Compile("namespace Contoso.Widgets.Bars; public sealed class Widget;"));
        var packFile = directory.Write(Path.Combine(LocalePackTestFactory.Folder, "Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var pack = LocalePackTestFactory.Resolve(directory, target, packFile);

        // Act
        var resolved = ProjectLocaleFiles.Resolve(target, [], [pack])!;

        // Assert
        var entry = Assert.Single(resolved.Read("Contoso.Widgets.Bars.Widget", CultureInfo.GetCultureInfo("he")));
        Assert.Multiple(
            () => Assert.Single(resolved.Packs),
            () => Assert.Empty(resolved.EmbeddedFiles),
            () => Assert.Equal([packFile], resolved.Files.Select(file => file.Path)),
            () => Assert.Equal(["he"], resolved.Cultures.Select(culture => culture.Name)),
            () => Assert.Equal("Today", entry.Key),
            () => Assert.Equal(packFile, resolved.GetFile(entry.FileName)!.Path),
            () => Assert.Equal(["Contoso.Widgets.Bars"], resolved.GetAnchorNames(resolved.Files[0])));
    }

    [Fact]
    public void Should_lay_a_pack_folder_out_by_the_root_namespace_the_project_declares()
    {
        // Arrange — the runtime counts the pack's anchors from the root namespace the project's own config declares, not from the assembly's name.
        using var directory = new TempDirectory();
        var config = directory.Write(Path.Combine("Widgets", ".locale.json"), """{ "rootNamespace": "Contoso.Components.Widgets" }""");
        var target = Target(directory, CompilationTestFactory.Building("Contoso.Widgets", "namespace Contoso.Components.Widgets.Bars; public sealed class Widget;"), new Dictionary<string, string> { [Path.GetFullPath(config)] = "Contoso.Widgets..locale.json" });
        var packFile = directory.Write(Path.Combine(LocalePackTestFactory.Folder, "Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var pack = LocalePackTestFactory.Resolve(directory, target, packFile);

        // Act
        var resolved = ProjectLocaleFiles.Resolve(target, [config], [pack])!;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["Contoso.Components.Widgets.Bars"], resolved.GetAnchorNames(resolved.Files[0])),
            () => Assert.Single(resolved.Read("Contoso.Components.Widgets.Bars.Widget", CultureInfo.GetCultureInfo("he"))));
    }

    [Fact]
    public void Should_read_the_projects_own_files_before_a_packs()
    {
        // Arrange — the runtime reads an assembly's embedded files first, then each pack, so a pack's entry overrides the assembly's own.
        using var directory = new TempDirectory();
        var own = directory.Write(Path.Combine("Widgets", "Bars", ".locales", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "היום"));
        var target = Target(directory, Compile("namespace Contoso.Widgets.Bars; public sealed class Widget;"), new Dictionary<string, string> { [Path.GetFullPath(own)] = "Contoso.Widgets.Bars..locales.he.json" });
        var packFile = directory.Write(Path.Combine(LocalePackTestFactory.Folder, "Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.WithSection("Widget", "Today", "מחר"));
        var pack = LocalePackTestFactory.Resolve(directory, target, packFile);

        // Act
        var resolved = ProjectLocaleFiles.Resolve(target, [own], [pack])!;

        // Assert
        var entries = resolved.Read("Contoso.Widgets.Bars.Widget", CultureInfo.GetCultureInfo("he"));
        Assert.Equal([own, packFile], entries.Select(entry => resolved.GetFile(entry.FileName)!.Path));
    }

    [Fact]
    public void Should_leave_a_pack_folder_naming_another_assembly_out()
    {
        // Arrange
        using var directory = new TempDirectory();
        var target = Target(directory, Compile("namespace Contoso.Widgets; public sealed class Widget;"));
        var packFile = directory.Write(Path.Combine(LocalePackTestFactory.Folder, "Contoso.Gadgets", "he.json"), LocalizationFileFakeData.Valid);
        var pack = LocalePackTestFactory.Resolve(directory, target, packFile);

        // Act
        var resolved = ProjectLocaleFiles.Resolve(target, [], [pack])!;

        // Assert
        Assert.Multiple(
            () => Assert.Empty(resolved.Packs),
            () => Assert.Empty(resolved.Files));
    }

    [Fact]
    public void Should_resolve_nothing_for_a_locale_pack()
    {
        // Arrange — a pack embeds its files for the assemblies its folders are named for, and declares no type to read them.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var resourceNames = new Dictionary<string, string> { [Path.GetFullPath(file)] = "Contoso.Widgets.Bars.he.json" };
        var project = new LoadedProject(Path.Combine(directory.Root, "Contoso.Widgets.Locales.Hebrew.csproj"), Compile(string.Empty), false, false, EmbeddedResourceNames: resourceNames, IsLocalePack: true);

        // Act
        var resolved = ProjectLocaleFiles.Resolve(project, [file]);

        // Assert
        Assert.Null(resolved);
    }

    private static Compilation Compile(string source)
        => CompilationTestFactory.Unreferenced(source);

    // A project a pack may name, in a folder of its own beside the pack's, building Contoso.Widgets.
    private static LoadedProject Target(TempDirectory directory, Compilation compilation, IReadOnlyDictionary<string, string>? resourceNames = null)
        => new(Path.Combine(directory.Root, "Widgets", "Contoso.Widgets.csproj"), compilation.WithAssemblyName("Contoso.Widgets"), false, false, EmbeddedResourceNames: resourceNames ?? new Dictionary<string, string>());

    private static ProjectLocalization Resolve(TempDirectory directory, Compilation compilation, params string[] files)
        => ProjectLocaleFiles.Resolve(new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), compilation, false, false), files)!;
}
