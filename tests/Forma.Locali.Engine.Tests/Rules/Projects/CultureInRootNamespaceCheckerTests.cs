namespace Forma.Locali.Tests.Rules.Projects;

using Forma.Locali.Projects;
using Forma.Locali.Rules.Projects;

public sealed class CultureInRootNamespaceCheckerTests
{
    [Fact]
    public void Should_report_an_assembly_name_with_a_part_that_is_a_culture_the_runtime_reads_an_embedded_file_for()
    {
        // Arrange — the build embeds under the assembly's name, so the file is read for 'my' rather than 'he'.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var project = Project(directory, "Contoso.my.Widgets", rootNamespace: null, file);

        // Act
        var diagnostic = Assert.Single(Check(project, file));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.CultureInRootNamespace, diagnostic.Code),
            () => Assert.Equal(project.Path, diagnostic.Path),
            () => Assert.Contains("'my' in 'Contoso.my.Widgets', the assembly's name and the root namespace", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains(Path.Combine("Components", ".locales", "he.json"), diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_report_a_root_namespace_apart_from_the_assembly_name_with_a_part_that_is_a_culture()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", ".locales", "es.json"), LocalizationFileFakeData.ForCulture("es", "Today", "Hoy"));
        var project = Project(directory, "Contoso.Widgets", rootNamespace: "Contoso.he.Widgets", file);

        // Act
        var diagnostic = Assert.Single(Check(project, file));

        // Assert
        Assert.Contains("'he' in 'Contoso.he.Widgets', the root namespace", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_read_a_name_without_the_folder_of_locale_files_its_config_names()
    {
        // Arrange — with '.strings' left out of the name, the runtime reads 'my' as the file's culture.
        using var directory = new TempDirectory();
        var config = directory.Write(".locale.json", """{ "localesDir": ".strings" }""");
        var file = directory.Write(Path.Combine("Components", ".strings", "he.json"), LocalizationFileFakeData.Valid);
        var project = Project(directory, "Contoso.my.Widgets", rootNamespace: null, config, file);

        // Act
        var diagnostics = Check(project, config, file);

        // Assert
        Assert.Equal([LintingCodes.CultureInRootNamespace], diagnostics.Codes());
    }

    [Fact]
    public void Should_report_a_root_namespace_a_name_an_item_of_the_projects_own_spells_with_dots_starts_with()
    {
        // Arrange — the runtime separates a name's parts by a folder and a dot alike, so it reads the SDK's name for the file for 'my' too.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var project = Project(directory, "Contoso.my.Widgets", rootNamespace: null, file) with
        {
            EmbeddedResourceNames = new Dictionary<string, string> { [Path.GetFullPath(file)] = "Contoso.my.Widgets.Components..locales.he.json" },
        };

        // Act
        var diagnostics = Check(project, file);

        // Assert
        Assert.Equal([LintingCodes.CultureInRootNamespace], diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_a_file_embedded_under_a_name_without_the_formats_extension_alone()
    {
        // Arrange — the runtime reads no name that does not end in the format's extension, whatever culture it holds.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var project = new LoadedProject(
            Path.Combine(directory.Root, "Contoso.my.Widgets.csproj"),
            CompilationTestFactory.Building("Contoso.my.Widgets"),
            false,
            false,
            EmbeddedResourceNames: new Dictionary<string, string> { [Path.GetFullPath(file)] = "Contoso.my.Widgets.Strings" });

        // Act
        var diagnostics = Check(project, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_project_embedding_no_locale_file_alone()
    {
        // Arrange — with no file embedded under the name, the runtime has nothing to misread.
        using var directory = new TempDirectory();
        var project = Project(directory, "Contoso.my.Widgets", rootNamespace: null);

        // Act
        var diagnostics = Check(project);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_an_assembly_name_with_a_culture_alone_where_the_root_namespace_holds_none()
    {
        // Arrange — the files are embedded under the root namespace, which holds no culture, so the project's own files are read rightly.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var project = Project(directory, "Contoso.my.Widgets", rootNamespace: "Contoso.Widgets", file);

        // Act
        var diagnostics = Check(project, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_part_that_is_a_culture_in_another_case_alone()
    {
        // Arrange — a culture is written exactly as .NET names it, so 'He' is no culture.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var project = Project(directory, "Contoso.He.Widgets", rootNamespace: null, file);

        // Act
        var diagnostics = Check(project, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_locale_pack_alone()
    {
        // Arrange — a pack embeds its files under their paths alone.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Contoso.Widgets", "he.json"), LocalizationFileFakeData.Valid);
        var project = Project(directory, "Contoso.Locales.my", rootNamespace: null, file) with { IsLocalePack = true };

        // Act
        var diagnostics = Check(project, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_project_that_did_not_compile_alone()
    {
        // Arrange — without a compilation the assembly's name cannot be told.
        using var directory = new TempDirectory();
        var file = directory.Write(Path.Combine("Components", ".locales", "he.json"), LocalizationFileFakeData.Valid);
        var project = Project(directory, "Contoso.my.Widgets", rootNamespace: null, file) with { Compilation = null };

        // Act
        var diagnostics = Check(project, file);

        // Assert
        Assert.Empty(diagnostics);
    }

    // The build embeds each file under the root namespace and its path, every folder joined by a /.
    private static LoadedProject Project(TempDirectory directory, string assemblyName, string? rootNamespace, params string[] files)
        => new(
            Path.Combine(directory.Root, $"{assemblyName}.csproj"),
            CompilationTestFactory.Building(assemblyName),
            false,
            false,
            rootNamespace,
            files.ToDictionary(Path.GetFullPath, file => $"{rootNamespace ?? assemblyName}/{Path.GetRelativePath(directory.Root, file).Replace(Path.DirectorySeparatorChar, '/')}", StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyList<LintingDiagnostic> Check(LoadedProject project, params string[] files)
        => ProjectLinterTestFactory.Check(new CultureInRootNamespaceChecker(), project, files);
}
