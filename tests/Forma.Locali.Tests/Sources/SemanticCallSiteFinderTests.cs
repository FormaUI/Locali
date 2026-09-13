namespace Forma.Locali.Tests.Sources;

public sealed class SemanticCallSiteFinderTests
{
    [Fact]
    public void Should_name_the_type_the_generic_contract_carries()
    {
        // Act
        var site = Assert.Single(Find("""
            private IStringLocale<Widget> L { get; } = default!;
            public void Show() => _ = L["Today"];
            """));

        // Assert
        Assert.Equal("Widget", site.ResourceType?.Name);
    }

    [Fact]
    public void Should_name_the_type_a_lookup_through_Resolve_carries()
    {
        // Act
        var site = Assert.Single(Find("""
            private IStringLocale<Widget> L { get; } = default!;
            public void Show() => _ = L.Resolve("Today");
            """));

        // Assert
        Assert.Equal("Widget", site.ResourceType?.Name);
    }

    [Fact]
    public void Should_name_no_type_for_the_unbound_contract()
    {
        // Act
        var site = Assert.Single(Find("""
            private IStringLocale L { get; } = default!;
            public void Show() => _ = L["Today"];
            """));

        // Assert
        Assert.Null(site.ResourceType);
    }

    [Fact]
    public void Should_name_the_open_definition_of_a_constructed_generic()
    {
        // Arrange — the factory reads a file by the open definition, because a closed one spells its arguments.
        var site = Assert.Single(Find("""
            private IStringLocale<Holder<int>> L { get; } = default!;
            public void Show() => _ = L["Today"];
            """));

        // Assert
        Assert.Equal("Holder`1", site.ResourceType?.MetadataName);
    }

    private static IReadOnlyList<LocalizerLookup> Find(string body)
    {
        var compilation = CompilationTestFactory.Referenced($$"""
            using Forma.Framework.Localization;

            namespace Scratch;

            public sealed class Holder<T>;

            public sealed class Widget
            {
                {{body}}
            }
            """);

        return [.. compilation.SyntaxTrees.SelectMany(tree => new SemanticCallSiteFinder().Find(compilation, tree).Lookups)];
    }
}
