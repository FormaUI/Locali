namespace Forma.Locali.Tests.Cli;

public sealed class LintingTargetResolverTests
{
    [Fact]
    public void Should_read_a_json_file_as_a_localization_file()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Valid);

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(file)).Resolve(file);

        // Assert
        Assert.Equal([file], target.LocalizationFiles);
    }

    [Fact]
    public void Should_read_a_source_file_as_a_source_file()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write("Widget.cs", "class Widget;");

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(file)).Resolve(file);

        // Assert
        Assert.Equal([file], target.SourceFiles);
    }

    [Fact]
    public void Should_read_a_Visual_Basic_file_as_a_source_file()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write("Widget.vb", "Class Widget\nEnd Class");

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(file)).Resolve(file);

        // Assert
        Assert.Equal([file], target.SourceFiles);
    }

    [Fact]
    public void Should_name_the_Visual_Basic_project_a_source_file_belongs_to()
    {
        // Arrange
        using var directory = new TempDirectory();
        var project = directory.Write("Scratch.vbproj", "<Project />");
        directory.Write("Nested/Widget.vb", "Class Widget\nEnd Class");

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(project)).Resolve(project);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([project], target.Projects),
            () => Assert.Single(target.SourceFiles));
    }

    [Fact]
    public void Should_read_razor_markup_as_a_source_file()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write("Widget.razor", "<p>x</p>");

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(file)).Resolve(file);

        // Assert
        Assert.Equal([file], target.SourceFiles);
    }

    [Fact]
    public void Should_scan_a_directory_for_every_kind_it_reads()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Valid);
        directory.Write("Widget.cs", "class Widget;");
        directory.Write("Widget.razor.cs", "class Other;");
        directory.Write("Widget.razor", "<p>x</p>");

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(directory.Root)).Resolve(directory.Root);

        // Assert
        Assert.Multiple(
            () => Assert.Single(target.LocalizationFiles),
            () => Assert.Equal(3, target.SourceFiles.Count));
    }

    [Fact]
    public void Should_ignore_build_output_when_scanning_a_directory()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write("bin/Debug/Widget.cs", "class Widget;");
        directory.Write("obj/Widget.razor", "<p>x</p>");

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(directory.Root)).Resolve(directory.Root);

        // Assert
        Assert.Empty(target.SourceFiles);
    }

    [Fact]
    public void Should_report_nothing_for_a_path_that_does_not_exist()
    {
        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(Path.GetTempPath())).Resolve(Path.Combine(Path.GetTempPath(), "forma-absent-a3f1"));

        // Assert
        Assert.Equal(0, target.FileCount);
    }

    [Fact]
    public void Should_name_the_project_a_source_file_belongs_to()
    {
        // Arrange
        using var directory = new TempDirectory();
        var project = directory.Write("Scratch.csproj", "<Project />");
        directory.Write("Nested/Widget.cs", "class Widget;");

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(directory.Root)).Resolve(directory.Root);

        // Assert
        Assert.Equal([project], target.Projects);
    }

    [Fact]
    public void Should_read_a_project_file_as_its_own_project()
    {
        // Arrange
        using var directory = new TempDirectory();
        var project = directory.Write("Scratch.csproj", "<Project />");
        directory.Write("Widget.cs", "class Widget;");

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(project)).Resolve(project);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([project], target.Projects),
            () => Assert.Single(target.SourceFiles),
            () => Assert.False(target.IsSolution));
    }

    [Fact]
    public void Should_read_a_solution_as_the_one_that_lists_every_project()
    {
        // Arrange
        using var directory = new TempDirectory();
        var solution = directory.Write("Scratch.slnx", "<Solution />");
        directory.Write(Path.Combine("Scratch", "Scratch.csproj"), "<Project />");
        directory.Write(Path.Combine("Scratch", "Widget.cs"), "class Widget;");

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(solution)).Resolve(solution);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([solution], target.Projects),
            () => Assert.Single(target.SourceFiles),
            () => Assert.True(target.IsSolution));
    }

    [Fact]
    public void Should_find_no_project_above_a_loose_source_file()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write("Widget.cs", "class Widget;");

        // Act
        var project = LintingTargetResolver.ProjectOf(file);

        // Assert
        Assert.Null(project);
    }

    [Fact]
    public void Should_attribute_a_locale_file_to_the_project_above_it()
    {
        // Arrange
        using var directory = new TempDirectory();
        var project = directory.Write("Scratch.csproj", "<Project />");
        var file = directory.Write(Path.Combine("Components", LocalizationFileFakeData.HebrewName), LocalizationFileFakeData.Valid);

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(directory.Root)).Resolve(directory.Root);

        // Assert
        Assert.Equal([file], target.LocalizationFilesOf(project));
    }

    [Fact]
    public void Should_attribute_a_locale_file_to_the_nearest_project_rather_than_its_parent()
    {
        // Arrange
        using var directory = new TempDirectory();
        var outer = directory.Write("Outer.csproj", "<Project />");
        var inner = directory.Write(Path.Combine("Nested", "Inner.csproj"), "<Project />");
        var file = directory.Write(Path.Combine("Nested", LocalizationFileFakeData.HebrewName), LocalizationFileFakeData.Valid);

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(directory.Root)).Resolve(directory.Root);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([file], target.LocalizationFilesOf(inner)),
            () => Assert.Empty(target.LocalizationFilesOf(outer)));
    }

    [Fact]
    public void Should_leave_an_excluded_file_unread()
    {
        // Arrange
        using var directory = new TempDirectory();
        var kept = directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Valid);
        directory.Write(Path.Combine("Fixtures", "Broken.he.json"), LocalizationFileFakeData.Unmarked);

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(directory.Root, ["*/Fixtures/*"])).Resolve(directory.Root);

        // Assert
        Assert.Equal([kept], target.LocalizationFiles);
    }

    [Fact]
    public void Should_match_an_exclude_however_the_path_is_written()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(Path.Combine("Fixtures", "Broken.he.json"), LocalizationFileFakeData.Unmarked);

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(directory.Root, ["*/fixtures/*"])).Resolve(directory.Root);

        // Assert
        Assert.Empty(target.LocalizationFiles);
    }

    [Fact]
    public void Should_exclude_source_files_as_well_as_locale_files()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(Path.Combine("Fixtures", "Widget.cs"), "class Widget;");

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(directory.Root, ["*/Fixtures/*"])).Resolve(directory.Root);

        // Assert
        Assert.Empty(target.SourceFiles);
    }

    [Fact]
    public void Should_read_everything_when_no_exclude_is_given()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(Path.Combine("Fixtures", "Broken.he.json"), LocalizationFileFakeData.Unmarked);

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(directory.Root)).Resolve(directory.Root);

        // Assert
        Assert.Single(target.LocalizationFiles);
    }

    [Fact]
    public void Should_apply_an_exclude_to_a_project_target()
    {
        // Arrange
        using var directory = new TempDirectory();
        var project = directory.Write("Scratch.csproj", "<Project />");
        var kept = directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Valid);
        directory.Write(Path.Combine("Fixtures", "Broken.he.json"), LocalizationFileFakeData.Unmarked);

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(project, ["*/Fixtures/*"])).Resolve(project);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([kept], target.LocalizationFiles),
            () => Assert.Equal([project], target.Projects));
    }

    [Fact]
    public void Should_leave_a_single_named_file_unread_when_it_is_excluded()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Fixtures", "Broken.he.json"), LocalizationFileFakeData.Unmarked);

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(file, ["*/Fixtures/*"])).Resolve(file);

        // Assert
        Assert.Empty(target.LocalizationFiles);
    }

    [Fact]
    public void Should_own_no_locale_files_when_none_sit_under_the_project()
    {
        // Arrange
        using var directory = new TempDirectory();
        var project = directory.Write("Scratch.csproj", "<Project />");
        directory.Write("Widget.cs", "class Widget;");

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(directory.Root)).Resolve(directory.Root);

        // Assert
        Assert.Empty(target.LocalizationFilesOf(project));
    }

    [Fact]
    public void Should_leave_out_a_file_the_runs_own_configuration_excludes()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "excludeFiles": ["Fixtures/*"] }""");
        directory.Write(Path.Combine("Fixtures", "Widget.he.json"), LocalizationFileFakeData.Valid);

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(directory.Root)).Resolve(directory.Root);

        // Assert
        Assert.Empty(target.LocalizationFiles);
    }

    [Fact]
    public void Should_never_read_the_runs_own_configuration_as_a_localization_file()
    {
        // Arrange — the file ends in .json like a locale file, but it is never one.
        using var directory = new TempDirectory();
        directory.Write(".locali.json", """{ "ignoreDiagnostics": {} }""");
        directory.Write(LocalizationFileFakeData.HebrewName, LocalizationFileFakeData.Valid);

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(directory.Root)).Resolve(directory.Root);

        // Assert
        Assert.Equal([LocalizationFileFakeData.HebrewName], target.LocalizationFiles.Select(Path.GetFileName));
    }
    [Fact]
    public void Should_list_every_configuration_file_the_walk_finds()
    {
        // Arrange — the run reads one configuration, and lists the rest so it can say they are not read.
        using var directory = new TempDirectory();
        var root = directory.Write(".locali.json", "{}");
        var nested = directory.Write(Path.Combine("Widgets", ".locali.json"), "{}");
        directory.Write("Widget.he.json", "{}");

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(directory.Root)).Resolve(directory.Root);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([root, nested], target.ConfigurationFiles.Order(StringComparer.Ordinal)),
            () => Assert.Single(target.LocalizationFiles));
    }

    [Fact]
    public void Should_leave_a_configuration_file_in_an_excluded_folder_unlisted()
    {
        // Arrange — a fixture folder may keep a .locali.json of its own to prove something, and an exclude covers it like the fixtures around it.
        using var directory = new TempDirectory();
        var root = directory.Write(".locali.json", "{}");
        directory.Write(Path.Combine("Fixtures", ".locali.json"), "not json");

        // Act
        var target = new LintingTargetResolver(new LintingConfiguration(directory.Root, ["*/Fixtures/*"])).Resolve(directory.Root);

        // Assert
        Assert.Equal([root], target.ConfigurationFiles);
    }
}
