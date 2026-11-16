namespace Forma.Locali.Tests.Sources;

using Microsoft.CodeAnalysis;

public sealed class LocaleSourceTypesTests
{
    private const string Components = """
        using System;

        using Forma.Framework.Localization;

        namespace Contoso.Components;

        public sealed class Composer;

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
        """;

    [Fact]
    public void Should_name_the_instances_own_type_a_property_another_project_of_the_run_builds_returns_through_GetType()
    {
        // Arrange
        var components = ComponentLibrary();
        var site = Find(components, "Component");

        // Act
        var read = Assert.Single(LocaleSourceTypes.ReadUnread([site], [components]));

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
        var site = Find(components, "Dialog");

        // Act
        var read = Assert.Single(LocaleSourceTypes.ReadUnread([site], [components]));

        // Assert
        Assert.Equal("Composer", Assert.Single(read.ResourceTypes).Name);
    }

    [Fact]
    public void Should_name_no_type_for_a_property_no_project_of_the_run_builds()
    {
        // Arrange
        var site = Find(ComponentLibrary(), "Component");

        // Act
        var read = Assert.Single(LocaleSourceTypes.ReadUnread([site], []));

        // Assert
        Assert.Empty(read.ResourceTypes);
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
        var read = Assert.Single(LocaleSourceTypes.ReadUnread([site], [compilation]));

        // Assert
        Assert.Same(site, read);
    }

    private static Compilation ComponentLibrary()
        => CompilationTestFactory.Referenced(Components).WithAssemblyName("Contoso.Components");

    // The lookup a type deriving from the base names writes, as a project reading the library's reference assembly finds it.
    private static LocalizerLookup Find(Compilation components, string baseName)
    {
        var compilation = CompilationTestFactory.Referenced($$"""
            namespace Scratch;

            public sealed class Widget : Contoso.Components.{{baseName}}
            {
                public void Show() => _ = L["Today"];
            }
            """).AddReferences(CompilationTestFactory.MetadataOf(components));

        return Assert.Single(compilation.SyntaxTrees.SelectMany(tree => new SemanticCallsiteFinder().Find(compilation, tree).Lookups));
    }
}
