namespace Forma.Locali.Tests.Sources;

using System.Globalization;

using Forma.Locali.Projects;

using Microsoft.CodeAnalysis;

public sealed class TranslationFilesTests
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
        var resolved = Assert.Single(TranslationFiles.Resolve(project, [file])!.Files);

        // Assert
        Assert.Equal("Forma.Framework.Emojis..locales.he.json", resolved.Name);
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
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), Compile("namespace Scratch;"), false, false, EmbeddedFiles: new HashSet<string>());

        // Act
        var resolved = TranslationFiles.Resolve(project, [file])!;

        // Assert
        Assert.Empty(resolved.Files);
    }

    [Fact]
    public void Should_resolve_an_embedded_file_in_a_locales_folder_at_the_project_root()
    {
        // Arrange — a folder of this name is the host's in one project and an embedded form in another; the project's items tell them apart.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "Widget.he.json"), LocalizationFileFakeData.Valid);
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), Compile("namespace Scratch;"), false, false, EmbeddedFiles: new HashSet<string> { Path.GetFullPath(file) });

        // Act
        var resolved = Assert.Single(TranslationFiles.Resolve(project, [file])!.Files);

        // Assert
        Assert.Equal("Scratch..locales.Widget.he.json", resolved.Name);
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
        var resolved = TranslationFiles.Resolve(project, [directory.Write(Path.Combine(".locales", "he.json"), LocalizationFileFakeData.Valid)]);

        // Assert
        Assert.Null(resolved);
    }

    private static Compilation Compile(string source)
        => CompilationTestFactory.Unreferenced(source);

    private static ProjectTranslations Resolve(TempDirectory directory, Compilation compilation, params string[] files)
        => TranslationFiles.Resolve(new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), compilation, false, false), files)!;
}
