namespace Forma.Locali.Tests.Sources;

using System.Globalization;

using Forma.Locali.Projects;

using Microsoft.CodeAnalysis;

public sealed class TranslationFilesTests
{
    private static readonly string ProjectPath = Path.Combine("C:", "Scratch", "Scratch.csproj");

    [Fact]
    public void Should_resolve_a_file_to_the_type_its_location_names()
    {
        // Arrange
        var compilation = Compile("""
            namespace Scratch.Components.Explorer;

            public sealed class FormaExplorer;
            """);
        var file = Path.Combine("C:", "Scratch", "Components", "Explorer", "FormaExplorer.he.json");

        // Act
        var resolved = Assert.Single(TranslationFiles.Resolve(Project(compilation), [file]));

        // Assert
        Assert.Equal("Scratch.Components.Explorer.FormaExplorer", resolved.Type?.ToDisplayString());
    }

    [Fact]
    public void Should_resolve_no_type_for_a_file_none_answers_to()
    {
        // Arrange
        var compilation = Compile("""
            namespace Scratch.Components.Explorer;

            public sealed class FormaExplorer;
            """);
        var file = Path.Combine("C:", "Scratch", "Components", "Explorer", "FormaGallery.he.json");

        // Act
        var resolved = Assert.Single(TranslationFiles.Resolve(Project(compilation), [file]));

        // Assert
        Assert.Multiple(
            () => Assert.Null(resolved.Type),
            () => Assert.Equal("Scratch.Components.Explorer.FormaGallery", resolved.ExpectedName));
    }

    [Fact]
    public void Should_carry_the_culture_the_file_name_gives_it()
    {
        // Arrange
        var file = Path.Combine("C:", "Scratch", "ResourceFormatter.es-419.json");

        // Act
        var resolved = Assert.Single(TranslationFiles.Resolve(Project(Compile("namespace Scratch;")), [file]));

        // Assert
        Assert.Equal(CultureInfo.GetCultureInfo("es-419"), resolved.Culture);
    }

    [Fact]
    public void Should_find_a_type_declared_at_the_project_root()
    {
        // Arrange
        var compilation = Compile("""
            namespace Scratch;

            public sealed class ResourceFormatter;
            """);
        var file = Path.Combine("C:", "Scratch", "ResourceFormatter.he.json");

        // Act
        var resolved = Assert.Single(TranslationFiles.Resolve(Project(compilation), [file]));

        // Assert
        Assert.NotNull(resolved.Type);
    }

    [Fact]
    public void Should_skip_a_file_carrying_no_recognised_culture()
    {
        // Arrange
        var file = Path.Combine("C:", "Scratch", "launchSettings.json");

        // Act
        var resolved = TranslationFiles.Resolve(Project(Compile("namespace Scratch;")), [file]);

        // Assert
        Assert.Empty(resolved);
    }

    [Fact]
    public void Should_shed_the_folders_a_resources_path_inserts()
    {
        // Arrange — the type sits at Fixtures, the file under Resources/Fixtures, which ResourcesPath inserts.
        var compilation = Compile("""
            namespace Scratch.Fixtures;

            public sealed class PathScoped;
            """);
        var file = Path.Combine("C:", "Scratch", "Resources", "Fixtures", "PathScoped.he.json");

        // Act
        var resolved = Assert.Single(TranslationFiles.Resolve(Project(compilation), [file]));

        // Assert
        Assert.NotNull(resolved.Type);
    }

    [Fact]
    public void Should_resolve_no_type_when_the_tail_names_none()
    {
        // Arrange — shedding every leading segment must not turn an absent type into a match.
        var compilation = Compile("""
            namespace Scratch.Fixtures;

            public sealed class PathScoped;
            """);
        var file = Path.Combine("C:", "Scratch", "Resources", "Fixtures", "Renamed.he.json");

        // Act
        var resolved = Assert.Single(TranslationFiles.Resolve(Project(compilation), [file]));

        // Assert
        Assert.Null(resolved.Type);
    }

    [Fact]
    public void Should_resolve_a_generic_type_whose_arity_the_file_name_drops()
    {
        // Arrange
        var compilation = Compile("""
            namespace Scratch.Fixtures;

            public sealed class GenericArity<TValue>;
            """);
        var file = Path.Combine("C:", "Scratch", "Fixtures", "GenericArity.he.json");

        // Act
        var resolved = Assert.Single(TranslationFiles.Resolve(Project(compilation), [file]));

        // Assert
        Assert.Equal("GenericArity`1", resolved.Type?.MetadataName);
    }

    [Fact]
    public void Should_resolve_a_type_in_the_global_namespace()
    {
        // Arrange
        var compilation = Compile("public sealed class GlobalRoot;");
        var file = Path.Combine("C:", "Scratch", "Fixtures", "GlobalRoot.he.json");

        // Act
        var resolved = Assert.Single(TranslationFiles.Resolve(Project(compilation), [file]));

        // Assert
        Assert.NotNull(resolved.Type);
    }

    [Fact]
    public void Should_read_the_root_namespace_rather_than_the_assembly_name()
    {
        // Arrange — an .Abstractions project embeds under the root its sibling shares, not its own name.
        var compilation = Compile("""
            namespace Forma.UI.Features.Feedback;

            public sealed class ToastMessage;
            """);
        var project = new LoadedProject(ProjectPath, compilation, false, false, "Forma.UI.Features");
        var file = Path.Combine("C:", "Scratch", "Feedback", "ToastMessage.he.json");

        // Act
        var resolved = Assert.Single(TranslationFiles.Resolve(project, [file]));

        // Assert
        Assert.NotNull(resolved.Type);
    }

    [Fact]
    public void Should_resolve_nothing_when_the_project_failed_to_load()
    {
        // Arrange
        var project = new LoadedProject(ProjectPath, null, false, false);
        var file = Path.Combine("C:", "Scratch", "FormaGallery.he.json");

        // Act
        var resolved = TranslationFiles.Resolve(project, [file]);

        // Assert
        Assert.Empty(resolved);
    }

    private static Compilation Compile(string source)
        => CompilationTestFactory.Unreferenced(source);

    private static LoadedProject Project(Compilation compilation)
        => new(ProjectPath, compilation, HasGeneratedSources: false, HasRazorMarkup: false);
}
