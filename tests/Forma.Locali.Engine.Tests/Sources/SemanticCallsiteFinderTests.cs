namespace Forma.Locali.Tests.Sources;

using Microsoft.CodeAnalysis;

public sealed class SemanticCallsiteFinderTests
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
        Assert.Equal("Widget", Assert.Single(site.Origins).Type.Name);
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
        Assert.Equal("Widget", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_name_the_type_behind_a_conditional_access()
    {
        // Arrange — the operation the compiler binds carries the receiver whether or not the access is conditional.
        var site = Assert.Single(Find("""
            private IStringLocale<Widget>? L { get; }
            public void Show() => _ = L?["Today"];
            """));

        // Assert
        Assert.Equal("Widget", Assert.Single(site.Origins).Type.Name);
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
        Assert.Empty(site.Origins);
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
        Assert.Equal("Holder`1", Assert.Single(site.Origins).Type.MetadataName);
    }

    [Fact]
    public void Should_read_a_lookup_through_a_Visual_Basic_default_property()
    {
        // Arrange — a C# indexer is a default property to Visual Basic, and the operation the compiler binds is the same one.
        var site = Assert.Single(FindVisualBasic("""
            Private ReadOnly Property L As IStringLocale(Of Widget)
            Public Sub Show()
                Dim text = L("Today")
            End Sub
            """));

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Today", site.Key.Text),
            () => Assert.Equal(1, site.Key.TextOffset),
            () => Assert.Equal("Widget", Assert.Single(site.Origins).Type.Name),
            () => Assert.Equal(LocalizerArguments.None, site.Arguments));
    }

    [Fact]
    public void Should_count_the_values_a_Visual_Basic_lookup_passes_to_a_param_array()
    {
        // Act
        var site = Assert.Single(FindVisualBasic("""
            Private ReadOnly Property L As IStringLocale(Of Widget)
            Public Sub Show()
                Dim text = L.Resolve("Stored {count}.", 1)
            End Sub
            """));

        // Assert
        Assert.Equal(LocalizerArguments.Positional(1), site.Arguments);
    }

    [Fact]
    public void Should_read_text_carried_as_data_in_the_open_definition_of_its_type_argument()
    {
        // Act
        var site = Assert.Single(Find("""public LocalizableText Title { get; } = LocalizableText.For<Holder<int>>("Stored {count}.", 1);"""));

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Stored {count}.", site.Key.Text),
            () => Assert.Equal(LocalizerArguments.Positional(1), site.Arguments),
            () => Assert.Equal("Holder`1", Assert.Single(site.Origins).Type.MetadataName));
    }

    [Fact]
    public void Should_read_text_carried_as_data_as_a_lookup()
    {
        // Act
        var site = Assert.Single(Find("""public LocalizableText Title { get; } = LocalizableText.For<Widget>("{Count} results today", 12);"""));

        // Assert
        Assert.Multiple(
            () => Assert.Equal("{Count} results today", site.Key.Text),
            () => Assert.Equal(LocalizerArguments.Positional(1), site.Arguments),
            () => Assert.Equal("Widget", Assert.Single(site.Origins).Type.Name));
    }

    [Fact]
    public void Should_leave_a_method_named_For_of_another_type_alone()
    {
        // Act — only the runtime's LocalizableText.For carries its key into a type's entries.
        var sites = Find("""
            private static string For<TSource>(string key) => key;
            public void Show() => _ = For<Widget>("Today");
            """);

        // Assert
        Assert.Empty(sites);
    }

    [Fact]
    public void Should_read_the_key_a_named_argument_writes_after_another()
    {
        // Act
        var site = Assert.Single(Find("""public LocalizableText Title { get; } = LocalizableText.For<Widget>(arguments: [1], key: "Stored {count}.");"""));

        // Assert
        Assert.Equal("Stored {count}.", site.Key.Text);
    }

    [Fact]
    public void Should_read_text_a_Visual_Basic_call_carries_as_data()
    {
        // Act
        var site = Assert.Single(FindVisualBasic("""
            Public Sub Show()
                Dim text = LocalizableText.For(Of Widget)("Stored {count}.", 1)
            End Sub
            """));

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Stored {count}.", site.Key.Text),
            () => Assert.Equal(LocalizerArguments.Positional(1), site.Arguments),
            () => Assert.Equal("Widget", Assert.Single(site.Origins).Type.Name));
    }

    private static IReadOnlyList<LocalizerLookup> FindVisualBasic(string body)
    {
        var compilation = CompilationTestFactory.ReferencedVisualBasic($"""
            Imports Forma.Framework.Localization

            Namespace Scratch
                Public NotInheritable Class Widget
                    {body}
                End Class
            End Namespace
            """);

        return [.. compilation.SyntaxTrees.SelectMany(tree => new SemanticCallsiteFinder().Find(compilation, tree).Lookups)];
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

        return [.. compilation.SyntaxTrees.SelectMany(tree => new SemanticCallsiteFinder().Find(compilation, tree).Lookups)];
    }
}
