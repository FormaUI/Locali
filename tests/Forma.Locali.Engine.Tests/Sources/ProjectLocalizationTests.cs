namespace Forma.Locali.Tests.Sources;

using Forma.Locali.Projects;

using Microsoft.CodeAnalysis;

public sealed class ProjectLocalizationTests
{
    [Fact]
    public void Should_name_a_referenced_assembly_a_files_name_starts_with()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "Forma.Framework.Localization", "Components", "he.json"), LocalizationFileFakeData.Valid);
        var localization = Resolve(directory, CompilationTestFactory.Referenced("namespace Scratch;"), file);

        // Act
        var named = localization.GetNamedAssemblies(Assert.Single(localization.Files));

        // Assert
        Assert.Equal(["Forma.Framework.Localization"], named);
    }

    [Fact]
    public void Should_name_the_projects_own_assembly_where_a_files_name_starts_with_it()
    {
        // Arrange — an application reads its own files for its own types by its own name too.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "es", "Scratch", "Components.json"), LocalizationFileFakeData.ForCulture("es", "Today", "Hoy"));
        var localization = Resolve(directory, CompilationTestFactory.Unreferenced("namespace Scratch;"), file);

        // Act
        var named = localization.GetNamedAssemblies(Assert.Single(localization.Files));

        // Assert
        Assert.Equal(["Scratch"], named);
    }

    [Fact]
    public void Should_name_an_assembly_a_project_of_the_run_builds()
    {
        // Arrange — a Web App's client may word its server's strings, an assembly it does not reference.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "Contoso.Server", "he.json"), LocalizationFileFakeData.Valid);
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Unreferenced("namespace Scratch;"), false, false);
        var localization = ProjectLocaleFiles.Resolve(LocaleFormatTestFactory.Json.RuntimeFormat, project, [file], builtAssemblyNames: ["Contoso.Server"])!;

        // Act
        var named = localization.GetNamedAssemblies(Assert.Single(localization.Files));

        // Assert
        Assert.Equal(["Contoso.Server"], named);
    }

    [Fact]
    public void Should_name_an_assembly_whatever_paths_the_projects_config_states()
    {
        // Arrange — the paths an application states read its own types, and its files for other assemblies are read by the default reading.
        using var directory = new TempDirectory();
        var config = directory.Write(".locale.json", """{ "paths": ["$RootDir/strings-$Culture/$AnchorName$Extension"] }""");
        var file = directory.Write(Path.Combine(".locales", "Forma.Framework.Localization", "he.json"), LocalizationFileFakeData.Valid);
        var resourceNames = new Dictionary<string, string>
        {
            [Path.GetFullPath(config)] = "Scratch..locale.json",
            [Path.GetFullPath(file)] = "Scratch..locales.Forma.Framework.Localization.he.json",
        };
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), CompilationTestFactory.Referenced("namespace Scratch;"), false, false, EmbeddedResourceNames: resourceNames);
        var localization = ProjectLocaleFiles.Resolve(LocaleFormatTestFactory.Json.RuntimeFormat, project, [file])!;

        // Act
        var named = localization.GetNamedAssemblies(Assert.Single(localization.Files));

        // Assert
        Assert.Equal(["Forma.Framework.Localization"], named);
    }

    [Fact]
    public void Should_name_no_assembly_for_a_file_laid_out_by_the_projects_own_namespaces()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var localization = Resolve(directory, CompilationTestFactory.Referenced("namespace Scratch.Components;"), file);

        // Act
        var named = localization.GetNamedAssemblies(Assert.Single(localization.Files));

        // Assert
        Assert.Empty(named);
    }

    private static ProjectLocalization Resolve(TempDirectory directory, Compilation compilation, params string[] files)
        => ProjectLocaleFiles.Resolve(LocaleFormatTestFactory.Json.RuntimeFormat, new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), compilation, false, false), files)!;
}
