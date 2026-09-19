namespace Forma.Locali.Tests.Sources;

using Forma.Locali.Projects;

public sealed class HostLocaleFilesTests
{
    [Fact]
    public void Should_place_a_hosts_file_by_the_assembly_folder_and_the_path_beneath_it()
    {
        // Arrange — the host's folder is .locales at the project's root, and the first folder inside it is the assembly's.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "Contoso.Widgets", "Bars", ".locales", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var placed = Assert.Single(Resolve(directory, file));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(file, placed.Path),
            () => Assert.Equal("he", placed.CultureName),
            () => Assert.Equal(["Contoso.Widgets.Bars"], placed.AnchorNames));
    }

    [Fact]
    public void Should_place_a_folders_file_directly_in_the_folder_at_the_same_anchor()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var placed = Assert.Single(Resolve(directory, file));

        // Assert
        Assert.Equal(["Contoso.Widgets.Bars"], placed.AnchorNames);
    }

    [Fact]
    public void Should_leave_a_file_the_project_embeds_to_the_assembly()
    {
        // Arrange — a root .locales folder a library embeds is the assembly's own, not a host's.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "he.json"), LocalizationFileFakeData.Valid);
        var resourceNames = new Dictionary<string, string> { [file] = "Scratch..locales.he.json" };

        // Act
        var placed = Resolve(directory, resourceNames, file);

        // Assert
        Assert.Empty(placed);
    }

    [Fact]
    public void Should_leave_a_file_outside_the_hosts_folder_alone()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Hosts", "Contoso.Widgets", ".locales", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var placed = Resolve(directory, file);

        // Assert
        Assert.Empty(placed);
    }

    [Fact]
    public void Should_leave_a_file_above_every_assemblys_folder_alone()
    {
        // Arrange — a file directly in the host's folder belongs to no assembly.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "he.json"), LocalizationFileFakeData.Valid);

        // Act
        var placed = Resolve(directory, file);

        // Assert
        Assert.Empty(placed);
    }

    [Fact]
    public void Should_read_no_host_file_without_the_projects_items()
    {
        // Arrange — without the items, every file is read as embedded.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine(".locales", "Contoso.Widgets", "he.json"), LocalizationFileFakeData.Valid);
        var project = new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), null, false, false);

        // Act
        var placed = HostLocaleFiles.Resolve(project, [file]);

        // Assert
        Assert.Empty(placed);
    }

    private static IReadOnlyList<PlacedLocaleFile> Resolve(TempDirectory directory, params string[] files)
        => Resolve(directory, new Dictionary<string, string>(), files);

    private static IReadOnlyList<PlacedLocaleFile> Resolve(TempDirectory directory, IReadOnlyDictionary<string, string> resourceNames, params string[] files)
        => HostLocaleFiles.Resolve(new LoadedProject(Path.Combine(directory.Root, "Scratch.csproj"), null, false, false, EmbeddedResourceNames: resourceNames), files);
}
