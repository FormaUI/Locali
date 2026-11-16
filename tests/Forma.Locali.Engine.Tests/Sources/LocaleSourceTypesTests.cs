namespace Forma.Locali.Tests.Sources;

using Forma.Locali.Projects;

using Microsoft.CodeAnalysis;

public sealed class LocaleSourceTypesTests
{
    private const string Components = """
        using System;

        using Forma.Framework.Localization;

        namespace Contoso.Components;

        public sealed class Composer;

        public sealed class Grid<TItem>;

        public abstract class Component
        {
            protected virtual Type LocalizationSourceType => GetType();

            [LocaleSource(nameof(LocalizationSourceType))]
            protected IStringLocale L => default!;
        }

        public abstract class Dialog : Component
        {
            protected override Type LocalizationSourceType => typeof(Composer);
        }

        public abstract class Row<TItem> : Component
        {
            protected override Type LocalizationSourceType => typeof(Grid<TItem>);
        }

        public abstract class Held : Component
        {
            private readonly Type _source = typeof(Composer);

            protected override Type LocalizationSourceType => _source;
        }
        """;

    [Fact]
    public void Should_name_the_instances_own_type_a_property_another_project_of_the_run_builds_returns_through_GetType()
    {
        // Arrange
        var components = ComponentLibrary();
        var (project, site) = Find(CompilationTestFactory.MetadataOf(components), "Component");

        // Act
        var read = Assert.Single(LocaleSourceTypes.ReadUnread(project, [site], [components]));

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Widget", Assert.Single(read.ResourceTypes).Name),
            () => Assert.Empty(read.UnreadProperties));
    }

    [Fact]
    public void Should_name_the_type_a_property_another_project_of_the_run_builds_returns_through_typeof()
    {
        // Arrange
        var components = ComponentLibrary();
        var (project, site) = Find(CompilationTestFactory.MetadataOf(components), "Dialog");

        // Act
        var read = Assert.Single(LocaleSourceTypes.ReadUnread(project, [site], [components]));

        // Assert
        Assert.Equal("Composer", Assert.Single(read.ResourceTypes).Name);
    }

    [Fact]
    public void Should_read_a_getter_a_project_of_the_run_builds_in_its_source_alone()
    {
        // Arrange — the assembly an earlier build left on disk names a type its source no longer does.
        using var directory = new TempDirectory();
        var built = ComponentLibrary(Components.Replace("=> _source;", "=> typeof(Composer);", StringComparison.Ordinal));
        var (project, site) = Find(CompilationTestFactory.WrittenTo(built, Path.Combine(directory.Root, "Contoso.Components.dll")), "Held");

        // Act
        var read = Assert.Single(LocaleSourceTypes.ReadUnread(project, [site], [ComponentLibrary()]));

        // Assert
        Assert.Multiple(
            () => Assert.Empty(read.ResourceTypes),
            () => Assert.Single(read.UnreadProperties));
    }

    [Fact]
    public void Should_name_the_instances_own_type_a_compiled_getter_no_project_of_the_run_builds_returns_through_GetType()
    {
        // Arrange — a package reaches a project as its compiled assembly alone.
        using var directory = new TempDirectory();
        var (project, site) = Find(CompilationTestFactory.WrittenTo(ComponentLibrary(), Path.Combine(directory.Root, "Contoso.Components.dll")), "Component");

        // Act
        var read = Assert.Single(LocaleSourceTypes.ReadUnread(project, [site], []));

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Widget", Assert.Single(read.ResourceTypes).Name),
            () => Assert.Empty(read.UnreadProperties));
    }

    [Fact]
    public void Should_name_the_type_a_compiled_getter_returns_through_typeof()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (project, site) = Find(CompilationTestFactory.WrittenTo(ComponentLibrary(), Path.Combine(directory.Root, "Contoso.Components.dll")), "Dialog");

        // Act
        var read = Assert.Single(LocaleSourceTypes.ReadUnread(project, [site], []));

        // Assert
        Assert.Equal("Composer", Assert.Single(read.ResourceTypes).Name);
    }

    [Fact]
    public void Should_name_the_open_definition_of_a_constructed_generic_a_compiled_getter_returns()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (project, site) = Find(CompilationTestFactory.WrittenTo(ComponentLibrary(), Path.Combine(directory.Root, "Contoso.Components.dll")), "Row<int>");

        // Act
        var read = Assert.Single(LocaleSourceTypes.ReadUnread(project, [site], []));

        // Assert
        Assert.Equal("Grid`1", Assert.Single(read.ResourceTypes).MetadataName);
    }

    [Fact]
    public void Should_read_a_getter_compiled_with_optimization_as_well_as_without()
    {
        // Arrange — an optimized build drops the local and the branch an unoptimized one writes around the return.
        using var directory = new TempDirectory();
        var components = ComponentLibrary().WithOptions(ComponentLibrary().Options.WithOptimizationLevel(OptimizationLevel.Release));
        var (project, site) = Find(CompilationTestFactory.WrittenTo(components, Path.Combine(directory.Root, "Contoso.Components.dll")), "Dialog");

        // Act
        var read = Assert.Single(LocaleSourceTypes.ReadUnread(project, [site], []));

        // Assert
        Assert.Equal("Composer", Assert.Single(read.ResourceTypes).Name);
    }

    [Fact]
    public void Should_read_a_compiled_getter_whose_body_is_a_block()
    {
        // Arrange
        using var directory = new TempDirectory();
        var components = ComponentLibrary(Components.Replace(
            "protected override Type LocalizationSourceType => typeof(Composer);",
            "protected override Type LocalizationSourceType { get { return typeof(Composer); } }",
            StringComparison.Ordinal));
        var (project, site) = Find(CompilationTestFactory.WrittenTo(components, Path.Combine(directory.Root, "Contoso.Components.dll")), "Dialog");

        // Act
        var read = Assert.Single(LocaleSourceTypes.ReadUnread(project, [site], []));

        // Assert
        Assert.Equal("Composer", Assert.Single(read.ResourceTypes).Name);
    }

    [Fact]
    public void Should_read_the_assembly_the_build_runs_in_place_of_a_reference_assembly()
    {
        // Arrange — the compiler reads a referenced project's reference assembly, and the build runs the assembly beside it.
        using var directory = new TempDirectory();
        var referencePath = Path.Combine(directory.Root, "ref", "Contoso.Components.dll");
        var runtimePath = Path.Combine(directory.Root, "Contoso.Components.dll");
        CompilationTestFactory.WrittenTo(ComponentLibrary(), runtimePath);
        var (project, site) = Find(CompilationTestFactory.WrittenTo(ComponentLibrary(), referencePath, metadataOnly: true), "Component");

        // Act
        var read = Assert.Single(LocaleSourceTypes.ReadUnread(
            project with { RuntimeAssemblyPaths = new Dictionary<string, string> { [referencePath] = runtimePath } },
            [site],
            []));

        // Assert
        Assert.Equal("Widget", Assert.Single(read.ResourceTypes).Name);
    }

    [Fact]
    public void Should_leave_unread_a_getter_a_reference_assembly_holds_with_nothing_in_its_place()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (project, site) = Find(CompilationTestFactory.WrittenTo(ComponentLibrary(), Path.Combine(directory.Root, "Contoso.Components.dll"), metadataOnly: true), "Component");

        // Act
        var read = Assert.Single(LocaleSourceTypes.ReadUnread(project, [site], []));

        // Assert
        Assert.Multiple(
            () => Assert.Empty(read.ResourceTypes),
            () => Assert.Equal("Widget", Assert.Single(read.UnreadProperties).Instance.Name));
    }

    [Fact]
    public void Should_leave_unread_a_compiled_getter_returning_a_type_it_holds()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (project, site) = Find(CompilationTestFactory.WrittenTo(ComponentLibrary(), Path.Combine(directory.Root, "Contoso.Components.dll")), "Held");

        // Act
        var read = Assert.Single(LocaleSourceTypes.ReadUnread(project, [site], []));

        // Assert
        Assert.Multiple(
            () => Assert.Empty(read.ResourceTypes),
            () => Assert.Single(read.UnreadProperties));
    }

    [Fact]
    public void Should_leave_a_lookup_holding_no_unread_property_as_it_is()
    {
        // Arrange
        var compilation = CompilationTestFactory.Referenced("""
            using Forma.Framework.Localization;

            namespace Scratch;

            public sealed class Widget
            {
                private IStringLocale<Widget> L { get; } = default!;

                public void Show() => _ = L["Today"];
            }
            """);
        var site = Assert.Single(compilation.SyntaxTrees.SelectMany(tree => new SemanticCallsiteFinder().Find(compilation, tree).Lookups));

        // Act
        var read = Assert.Single(LocaleSourceTypes.ReadUnread(new LoadedProject("Scratch.csproj", compilation, false, false), [site], [compilation]));

        // Assert
        Assert.Same(site, read);
    }

    private static Compilation ComponentLibrary(string source = Components)
        => CompilationTestFactory.Referenced(source).WithAssemblyName("Contoso.Components");

    // The lookup a type deriving from the base named writes, as a project reading the library's assembly finds it.
    private static (LoadedProject Project, LocalizerLookup Site) Find(MetadataReference components, string baseName)
    {
        var compilation = CompilationTestFactory.Referenced($$"""
            namespace Scratch;

            public sealed class Widget : Contoso.Components.{{baseName}}
            {
                public void Show() => _ = L["Today"];
            }
            """).AddReferences(components);

        var site = Assert.Single(compilation.SyntaxTrees.SelectMany(tree => new SemanticCallsiteFinder().Find(compilation, tree).Lookups));

        return (new LoadedProject("Scratch.csproj", compilation, false, false), site);
    }
}
