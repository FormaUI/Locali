namespace Forma.Locali.Tests.Cli;

public sealed class LintingTargetTests
{
    [Fact]
    public void Should_keep_the_files_a_project_of_the_run_owns()
    {
        // Arrange
        using var directory = new TempDirectory();
        var project = directory.Write(Path.Combine("Widgets", "Widgets.csproj"), "<Project />");
        var source = directory.Write(Path.Combine("Widgets", "Widget.cs"), "class Widget;");
        var file = directory.Write(Path.Combine("Widgets", ".locales", LocalizationFileFakeData.HebrewName), LocalizationFileFakeData.Valid);
        var target = Resolve(directory);

        // Act
        var narrowed = target.Within(Set(project), Set());

        // Assert
        Assert.Multiple(
            () => Assert.Equal([source], narrowed.SourceFiles),
            () => Assert.Equal([file], narrowed.LocalizationFiles),
            () => Assert.Equal([file], narrowed.LocalizationFilesOf(project)));
    }

    [Fact]
    public void Should_leave_out_the_files_of_a_project_the_run_did_not_load()
    {
        // Arrange — a throwaway project in a sandbox beside the solution's.
        using var directory = new TempDirectory();
        var project = directory.Write(Path.Combine("Widgets", "Widgets.csproj"), "<Project />");
        var throwaway = directory.Write(Path.Combine(".sandbox", "Throwaway", "Throwaway.csproj"), "<Project />");
        directory.Write(Path.Combine(".sandbox", "Throwaway", "Probe.cs"), "class Probe;");
        directory.Write(Path.Combine(".sandbox", "Throwaway", ".locales", LocalizationFileFakeData.HebrewName), LocalizationFileFakeData.Unmarked);
        var target = Resolve(directory);

        // Act
        var narrowed = target.Within(Set(project), Set());

        // Assert
        Assert.Multiple(
            () => Assert.Empty(narrowed.SourceFiles),
            () => Assert.Empty(narrowed.LocalizationFiles),
            () => Assert.Empty(narrowed.LocalizationFilesOf(throwaway)));
    }

    [Fact]
    public void Should_leave_out_a_file_no_project_sits_above()
    {
        // Arrange
        using var directory = new TempDirectory();
        var project = directory.Write(Path.Combine("Widgets", "Widgets.csproj"), "<Project />");
        directory.Write(Path.Combine(".sandbox", ".locales", LocalizationFileFakeData.HebrewName), LocalizationFileFakeData.Unmarked);
        directory.Write(Path.Combine(".sandbox", "Probe.cs"), "class Probe;");
        var target = Resolve(directory);

        // Act
        var narrowed = target.Within(Set(project), Set());

        // Assert
        Assert.Equal(0, narrowed.FileCount);
    }

    [Fact]
    public void Should_keep_a_file_a_project_of_the_run_builds_from_outside_its_folder()
    {
        // Arrange — a project may compile or embed a file through a link to a folder no project owns.
        using var directory = new TempDirectory();
        var project = directory.Write(Path.Combine("Widgets", "Widgets.csproj"), "<Project />");
        var linked = directory.Write(Path.Combine("Shared", "Linked.cs"), "class Linked;");
        var target = Resolve(directory);

        // Act
        var narrowed = target.Within(Set(project), Set(linked));

        // Assert
        Assert.Equal([linked], narrowed.SourceFiles);
    }

    [Fact]
    public void Should_keep_a_configuration_file_among_the_projects_of_the_run_or_above_one()
    {
        // Arrange
        using var directory = new TempDirectory();
        var project = directory.Write(Path.Combine("src", "Widgets", "Widgets.csproj"), "<Project />");
        var above = directory.Write(Path.Combine("src", ".locali.json"), "{}");
        var among = directory.Write(Path.Combine("src", "Widgets", "Legacy", ".locali.json"), "{}");
        directory.Write(Path.Combine(".sandbox", "Probe", ".locali.json"), "{}");
        var target = Resolve(directory);

        // Act
        var narrowed = target.Within(Set(project), Set());

        // Assert
        Assert.Equal([above, among], narrowed.ConfigurationFiles.Order(StringComparer.Ordinal));
    }

    private static LintingTarget Resolve(TempDirectory directory)
        => new LintingTargetResolver(new LintingConfiguration(directory.Root), LocaleFormatTestFactory.Json).Resolve(directory.Root);

    private static HashSet<string> Set(params string[] paths)
        => paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
}
