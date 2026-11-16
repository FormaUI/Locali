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
        Assert.Equal("Widget", Assert.Single(site.ResourceTypes).Name);
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
        Assert.Equal("Widget", Assert.Single(site.ResourceTypes).Name);
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
        Assert.Equal("Widget", Assert.Single(site.ResourceTypes).Name);
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
        Assert.Empty(site.ResourceTypes);
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
        Assert.Equal("Holder`1", Assert.Single(site.ResourceTypes).MetadataName);
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
            () => Assert.Equal("Widget", Assert.Single(site.ResourceTypes).Name),
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
    public void Should_read_a_call_to_a_method_marking_its_key_as_a_lookup_in_its_type_argument()
    {
        // Act
        var site = Assert.Single(Find("""
            private static string Label<TSource>([LocaleKey] string key, params object?[] values) => key;
            public void Show() => _ = Label<Holder<int>>("Stored {count}.", 1);
            """));

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Stored {count}.", site.Key.Text),
            () => Assert.Equal(LocalizerArguments.Positional(1), site.Arguments),
            () => Assert.Equal("Holder`1", Assert.Single(site.ResourceTypes).MetadataName));
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
            () => Assert.Equal("Widget", Assert.Single(site.ResourceTypes).Name));
    }

    [Fact]
    public void Should_read_a_call_to_a_method_a_referenced_assembly_marks()
    {
        // Arrange — a host's own method reaches the project that calls it as metadata.
        var library = CompilationTestFactory.Referenced("""
            using Forma.Framework.Localization;

            namespace Contoso.Labels;

            public static class Labels
            {
                public static string Label<TSource>([LocaleKey] string key) => key;
            }
            """).WithAssemblyName("Contoso.Labels");
        using var image = new MemoryStream();
        Assert.True(library.Emit(image, cancellationToken: TestContext.Current.CancellationToken).Success);

        var compilation = CompilationTestFactory.Referenced("""
            namespace Scratch;

            public sealed class Widget
            {
                public void Show() => _ = Contoso.Labels.Labels.Label<Widget>("Today");
            }
            """).AddReferences(MetadataReference.CreateFromImage(image.ToArray()));

        // Act
        var site = Assert.Single(compilation.SyntaxTrees.SelectMany(tree => new SemanticCallsiteFinder().Find(compilation, tree).Lookups));

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Today", site.Key.Text),
            () => Assert.Equal("Widget", Assert.Single(site.ResourceTypes).Name));
    }

    [Fact]
    public void Should_name_no_type_for_a_call_to_a_method_without_one_type_argument()
    {
        // Act
        var site = Assert.Single(Find("""
            private static string Label([LocaleKey] string key) => key;
            public void Show() => _ = Label("Today");
            """));

        // Assert
        Assert.Empty(site.ResourceTypes);
    }

    [Fact]
    public void Should_leave_the_values_of_a_call_uncounted_where_no_params_parameter_follows_its_key()
    {
        // Act — the method may fill the key's holes from values of its own.
        var site = Assert.Single(Find("""
            private static string Count<TSource>([LocaleKey] string key) => key;
            public void Show() => _ = Count<Widget>("{Count} items");
            """));

        // Assert
        Assert.Equal(LocalizerArguments.Unknown, site.Arguments);
    }

    [Fact]
    public void Should_read_the_key_a_named_argument_writes_after_another()
    {
        // Act
        var site = Assert.Single(Find("""
            private static string Label<TSource>(int count, [LocaleKey] string key) => key;
            public void Show() => _ = Label<Widget>(key: "Today", count: 1);
            """));

        // Assert
        Assert.Equal("Today", site.Key.Text);
    }

    [Fact]
    public void Should_read_a_Visual_Basic_call_to_a_method_marking_its_key()
    {
        // Act
        var site = Assert.Single(FindVisualBasic("""
            Private Shared Function Label(Of TSource)(<LocaleKey> key As String, ParamArray values As Object()) As String
                Return key
            End Function
            Public Sub Show()
                Dim text = Label(Of Widget)("Stored {count}.", 1)
            End Sub
            """));

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Stored {count}.", site.Key.Text),
            () => Assert.Equal(LocalizerArguments.Positional(1), site.Arguments),
            () => Assert.Equal("Widget", Assert.Single(site.ResourceTypes).Name));
    }

    [Fact]
    public void Should_name_the_instances_own_type_for_a_lookup_through_a_member_whose_named_property_returns_GetType()
    {
        // Act
        var site = Assert.Single(FindThroughComponent("""
            public sealed class Widget : Component
            {
                public void Show() => _ = L["Today"];
            }
            """));

        // Assert
        Assert.Equal("Widget", Assert.Single(site.ResourceTypes).Name);
    }

    [Fact]
    public void Should_name_the_type_an_override_of_the_named_property_returns_through_typeof()
    {
        // Act
        var site = Assert.Single(FindThroughComponent("""
            public sealed class Panel;

            public sealed class Widget : Component
            {
                protected override Type LocalizationSourceType => typeof(Panel);

                public void Show() => _ = L["Today"];
            }
            """));

        // Assert
        Assert.Equal("Panel", Assert.Single(site.ResourceTypes).Name);
    }

    [Fact]
    public void Should_name_the_open_definition_of_a_constructed_generic_the_named_property_returns()
    {
        // Act
        var site = Assert.Single(FindThroughComponent("""
            public sealed class Grid<TItem>;

            public sealed class Row<TItem> : Component
            {
                protected override Type LocalizationSourceType => typeof(Grid<TItem>);

                public void Show() => _ = L["Today"];
            }
            """));

        // Assert
        Assert.Equal("Grid`1", Assert.Single(site.ResourceTypes).MetadataName);
    }

    [Fact]
    public void Should_read_a_named_property_whose_getter_returns_once_from_a_block()
    {
        // Act
        var site = Assert.Single(FindThroughComponent("""
            public sealed class Panel;

            public sealed class Widget : Component
            {
                protected override Type LocalizationSourceType
                {
                    get
                    {
                        return typeof(Panel);
                    }
                }

                public void Show() => _ = L["Today"];
            }
            """));

        // Assert
        Assert.Equal("Panel", Assert.Single(site.ResourceTypes).Name);
    }

    [Fact]
    public void Should_name_the_type_behind_a_conditional_access_to_a_member_carrying_LocaleSource()
    {
        // Act
        var site = Assert.Single(FindThroughComponent("""
            public sealed class Widget : Component
            {
                public void Show() => _ = L?["Today"];
            }
            """));

        // Assert
        Assert.Equal("Widget", Assert.Single(site.ResourceTypes).Name);
    }

    [Fact]
    public void Should_name_the_type_each_derived_type_reads_for_a_lookup_it_runs()
    {
        // Arrange — an instance of a derived type runs the lookup its base writes, and reads the type its own named property returns.
        var site = Assert.Single(FindThroughComponent("""
            public sealed class Panel;

            public class Widget : Component
            {
                public void Show() => _ = L["Today"];
            }

            public sealed class Gadget : Widget;

            public sealed class Gizmo : Widget
            {
                protected override Type LocalizationSourceType => typeof(Panel);
            }
            """));

        // Assert
        Assert.Equal(["Gadget", "Panel", "Widget"], site.ResourceTypes.Select(type => type.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Should_leave_out_a_derived_type_overriding_the_member_holding_the_lookup()
    {
        // Act
        var site = Assert.Single(FindThroughComponent("""
            public class Widget : Component
            {
                public virtual void Show() => _ = L["Today"];
            }

            public sealed class Gadget : Widget
            {
                public override void Show()
                {
                }
            }
            """));

        // Assert
        Assert.Equal("Widget", Assert.Single(site.ResourceTypes).Name);
    }

    [Fact]
    public void Should_leave_out_a_derived_type_overriding_the_property_whose_getter_holds_the_lookup()
    {
        // Act
        var site = Assert.Single(FindThroughComponent("""
            public class Widget : Component
            {
                public virtual string Label => L["Today"];
            }

            public sealed class Gadget : Widget
            {
                public override string Label => "Now";
            }
            """));

        // Assert
        Assert.Equal("Widget", Assert.Single(site.ResourceTypes).Name);
    }

    [Fact]
    public void Should_leave_out_an_abstract_type_no_instance_has()
    {
        // Act
        var site = Assert.Single(FindThroughComponent("""
            public abstract class Widget : Component
            {
                public void Show() => _ = L["Today"];
            }

            public sealed class Gadget : Widget;
            """));

        // Assert
        Assert.Equal("Gadget", Assert.Single(site.ResourceTypes).Name);
    }

    [Fact]
    public void Should_name_no_type_where_the_named_property_returns_a_type_it_holds()
    {
        // Act
        var site = Assert.Single(FindThroughComponent("""
            public sealed class Widget : Component
            {
                private readonly Type _source = typeof(Widget);

                protected override Type LocalizationSourceType => _source;

                public void Show() => _ = L["Today"];
            }
            """));

        // Assert
        Assert.Empty(site.ResourceTypes);
    }

    [Fact]
    public void Should_name_no_type_where_the_named_property_returns_more_than_once()
    {
        // Act
        var site = Assert.Single(FindThroughComponent("""
            public sealed class Panel;

            public sealed class Widget : Component
            {
                public bool IsPanel { get; set; }

                protected override Type LocalizationSourceType
                {
                    get
                    {
                        if (IsPanel)
                        {
                            return typeof(Panel);
                        }

                        return typeof(Widget);
                    }
                }

                public void Show() => _ = L["Today"];
            }
            """));

        // Assert
        Assert.Empty(site.ResourceTypes);
    }

    [Fact]
    public void Should_name_no_type_for_a_lookup_through_another_instance()
    {
        // Act
        var site = Assert.Single(FindThroughComponent("""
            public sealed class Widget : Component
            {
                public void Show(Widget other) => _ = other.L["Today"];
            }
            """));

        // Assert
        Assert.Empty(site.ResourceTypes);
    }

    [Fact]
    public void Should_leave_unread_a_named_property_whose_getter_a_referenced_assembly_holds()
    {
        // Arrange — the build hands a project the reference assembly of another, which holds no getter's body.
        var compilation = ReferencingMetadataOf(ComponentLibrary());

        // Act
        var site = Assert.Single(compilation.SyntaxTrees.SelectMany(tree => new SemanticCallsiteFinder().Find(compilation, tree).Lookups));

        // Assert
        var unread = Assert.Single(site.UnreadProperties);
        Assert.Multiple(
            () => Assert.Empty(site.ResourceTypes),
            () => Assert.Equal("Widget", unread.Instance.Name),
            () => Assert.Equal("LocalizationSourceType", unread.Property.Name));
    }

    [Fact]
    public void Should_name_the_instances_own_type_a_Visual_Basic_base_returns_through_GetType()
    {
        // Act
        var site = Assert.Single(FindVisualBasicThroughComponent("""
            Public NotInheritable Class Widget
                Inherits Component
                Public Sub Show()
                    Dim text = L("Today")
                End Sub
            End Class
            """));

        // Assert
        Assert.Equal("Widget", Assert.Single(site.ResourceTypes).Name);
    }

    [Fact]
    public void Should_name_the_type_a_Visual_Basic_override_returns_through_GetType_of_a_type()
    {
        // Act
        var site = Assert.Single(FindVisualBasicThroughComponent("""
            Public NotInheritable Class Panel
            End Class

            Public NotInheritable Class Widget
                Inherits Component
                Protected Overrides ReadOnly Property LocalizationSourceType As Type
                    Get
                        Return GetType(Panel)
                    End Get
                End Property
                Public Sub Show()
                    Dim text = L("Today")
                End Sub
            End Class
            """));

        // Assert
        Assert.Equal("Panel", Assert.Single(site.ResourceTypes).Name);
    }

    private const string Component = """
        public abstract class Component
        {
            protected virtual Type LocalizationSourceType => GetType();

            [LocaleSource(nameof(LocalizationSourceType))]
            protected IStringLocale L => default!;
        }
        """;

    private static Compilation ComponentLibrary()
        => CompilationTestFactory.Referenced($$"""
            using System;

            using Forma.Framework.Localization;

            namespace Contoso.Components;

            {{Component}}
            """).WithAssemblyName("Contoso.Components");

    private static Compilation ReferencingMetadataOf(Compilation components)
        => CompilationTestFactory.Referenced("""
            namespace Scratch;

            public sealed class Widget : Contoso.Components.Component
            {
                public void Show() => _ = L["Today"];
            }
            """).AddReferences(CompilationTestFactory.MetadataOf(components));

    private static IReadOnlyList<LocalizerLookup> FindThroughComponent(string declarations)
    {
        var compilation = CompilationTestFactory.Referenced($$"""
            using System;

            using Forma.Framework.Localization;

            namespace Scratch;

            {{Component}}

            {{declarations}}
            """);

        return [.. compilation.SyntaxTrees.SelectMany(tree => new SemanticCallsiteFinder().Find(compilation, tree).Lookups)];
    }

    private static IReadOnlyList<LocalizerLookup> FindVisualBasicThroughComponent(string declarations)
    {
        var compilation = CompilationTestFactory.ReferencedVisualBasic($"""
            Imports System

            Imports Forma.Framework.Localization

            Namespace Scratch
                Public MustInherit Class Component
                    Protected Overridable ReadOnly Property LocalizationSourceType As Type
                        Get
                            Return Me.GetType()
                        End Get
                    End Property
                    <LocaleSource(NameOf(LocalizationSourceType))>
                    Protected ReadOnly Property L As IStringLocale
                        Get
                            Return Nothing
                        End Get
                    End Property
                End Class

                {declarations}
            End Namespace
            """);

        return [.. compilation.SyntaxTrees.SelectMany(tree => new SemanticCallsiteFinder().Find(compilation, tree).Lookups)];
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
