namespace Forma.Locali.Tests.Flow;

using Forma.Locali.Projects;

using Microsoft.CodeAnalysis;

public sealed class LocaleFlowTests
{
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
        Assert.Equal("Widget", Assert.Single(site.Origins).Type.Name);
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
        Assert.Equal("Panel", Assert.Single(site.Origins).Type.Name);
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
        Assert.Equal("Grid`1", Assert.Single(site.Origins).Type.MetadataName);
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
        Assert.Equal("Panel", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_name_the_type_behind_a_conditional_access_to_a_hinted_member()
    {
        // Act
        var site = Assert.Single(FindThroughComponent("""
            public sealed class Widget : Component
            {
                public void Show() => _ = L?["Today"];
            }
            """));

        // Assert
        Assert.Equal("Widget", Assert.Single(site.Origins).Type.Name);
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
        Assert.Equal(["Gadget", "Panel", "Widget"], site.Origins.Select(origin => origin.Type.Name).Order(StringComparer.Ordinal));
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
        Assert.Equal("Widget", Assert.Single(site.Origins).Type.Name);
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
        Assert.Equal("Widget", Assert.Single(site.Origins).Type.Name);
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
        Assert.Equal("Gadget", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_read_the_type_a_named_property_returns_from_a_field_written_once()
    {
        // Act
        var site = Assert.Single(FindThroughComponent("""
            public sealed class Panel;

            public sealed class Widget : Component
            {
                private readonly Type _source = typeof(Panel);

                protected override Type LocalizationSourceType => _source;

                public void Show() => _ = L["Today"];
            }
            """));

        // Assert
        Assert.Equal("Panel", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_name_no_type_where_the_named_property_returns_one_read_from_an_array()
    {
        // Act
        var site = Assert.Single(FindThroughComponent("""
            public sealed class Widget : Component
            {
                private readonly Type[] _sources = [typeof(Widget)];

                protected override Type LocalizationSourceType => _sources[0];

                public void Show() => _ = L["Today"];
            }
            """));

        // Assert
        Assert.Multiple(
            () => Assert.Empty(site.Origins),
            () => Assert.Equal(LocaleGapKind.UnreadTypeMember, Assert.Single(site.Gaps).Kind));
    }

    [Fact]
    public void Should_name_only_approximate_types_where_the_named_property_returns_more_than_once()
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

        // Assert — either return can be the one taken, so neither pairs with the lookup for certain.
        Assert.Multiple(
            () => Assert.Equal(["Panel", "Widget"], site.Origins.Select(origin => origin.Type.Name).Order(StringComparer.Ordinal)),
            () => Assert.All(site.Origins, origin => Assert.True(origin.IsApproximate)));
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
        Assert.Empty(site.Origins);
    }

    [Fact]
    public void Should_leave_unread_a_named_property_whose_getter_a_reference_assembly_holds_alone()
    {
        // Arrange — the build hands a project the reference assembly of another, which holds no getter's body.
        var compilation = ReferencingMetadataOf(ComponentLibrary());

        // Act
        var site = Assert.Single(LocaleFlowTestFactory.Resolve([LibraryHint], compilation));

        // Assert
        var gap = Assert.Single(site.Gaps);
        Assert.Multiple(
            () => Assert.Empty(site.Origins),
            () => Assert.Equal(LocaleGapKind.UnreadTypeMember, gap.Kind),
            () => Assert.Equal("Component.LocalizationSourceType for Widget", gap.Detail));
    }

    [Fact]
    public void Should_read_the_named_property_of_a_base_another_project_of_the_run_builds_in_its_source()
    {
        // Arrange
        var components = ComponentLibrary();
        var compilation = ReferencingMetadataOf(components);

        // Act
        var site = Assert.Single(LocaleFlowTestFactory.Resolve([LibraryHint], compilation, components));

        // Assert
        Assert.Equal("Widget", Assert.Single(site.Origins).Type.Name);
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
        Assert.Equal("Widget", Assert.Single(site.Origins).Type.Name);
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
        Assert.Equal("Panel", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_name_the_type_a_helper_is_called_with_at_its_callers()
    {
        // Act — the helper's locale is the one each caller passes.
        var site = Single(Resolve("""
            internal static class Labels
            {
                public static string Folder(IStringLocale locale) => locale["Folder"].Value;
            }

            public sealed class Widget
            {
                private IStringLocale<Widget> L { get; } = default!;

                public string Show() => Labels.Folder(L);
            }
            """), "Folder");

        // Assert
        Assert.Equal("Widget", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_name_each_type_a_shared_helper_is_called_with()
    {
        // Act — each caller is a path the helper's lookup really runs on.
        var site = Single(Resolve("""
            internal static class Labels
            {
                public static string Folder(IStringLocale locale) => locale["Folder"].Value;
            }

            public sealed class Widget
            {
                private IStringLocale<Widget> L { get; } = default!;

                public string Show() => Labels.Folder(L);
            }

            public sealed class Gadget
            {
                private IStringLocale<Gadget> L { get; } = default!;

                public string Show() => Labels.Folder(L);
            }
            """), "Folder");

        // Assert
        Assert.Equal(["Gadget", "Widget"], site.Origins.Select(origin => origin.Type.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Should_keep_apart_the_types_two_callers_pass_through_one_shared_method()
    {
        // Act — each caller reads the locale its own call returned, never the other's.
        var sites = Resolve("""
            internal static class Locales
            {
                public static IStringLocale Make(IServiceProvider services, Type source) => services.GetStringLocaleFactory().Create(source);
            }

            public sealed class Widget(IServiceProvider services)
            {
                public string Show() => Locales.Make(services, typeof(Widget))["Widget's"].Value;
            }

            public sealed class Gadget(IServiceProvider services)
            {
                public string Show() => Locales.Make(services, typeof(Gadget))["Gadget's"].Value;
            }
            """);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Widget", Assert.Single(Single(sites, "Widget's").Origins).Type.Name),
            () => Assert.Equal("Gadget", Assert.Single(Single(sites, "Gadget's").Origins).Type.Name));
    }

    [Fact]
    public void Should_name_the_type_a_model_is_constructed_with()
    {
        // Act — the page passes its locale to the model it builds, which reads it in a method of its own.
        var site = Single(Resolve("""
            internal sealed class Model(IStringLocale localizer)
            {
                public string Error() => localizer["A title is required."].Value;
            }

            public sealed class Page
            {
                private IStringLocale<Page> L { get; } = default!;

                public object Build() => new Model(L);
            }
            """), "A title is required.");

        // Assert
        Assert.Equal("Page", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_read_each_object_by_the_locale_it_was_constructed_with()
    {
        // Act — two holders keep their own locales, and the lookup reads the one it is made through.
        var site = Single(Resolve("""
            internal sealed class Holder
            {
                private readonly IStringLocale _locale;

                public Holder(IStringLocale locale) => _locale = locale;

                public IStringLocale Locale => _locale;
            }

            public sealed class Page(IStringLocale<Page> pageLocale, IStringLocale<Panel> panelLocale)
            {
                public string Show()
                {
                    var page = new Holder(pageLocale);
                    var panel = new Holder(panelLocale);

                    return page.Locale["Page's"].Value;
                }
            }

            public sealed class Panel;
            """), "Page's");

        // Assert
        Assert.Equal("Page", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_read_an_object_by_the_locale_its_chained_constructor_was_given()
    {
        // Act — the constructor called passes its locale on to the one that stores it.
        var site = Single(Resolve("""
            internal sealed class Holder
            {
                private readonly IStringLocale _locale;

                public Holder(IStringLocale locale)
                    : this(locale, 0)
                {
                }

                private Holder(IStringLocale locale, int order) => _locale = locale;

                public IStringLocale Locale => _locale;
            }

            public sealed class Page(IStringLocale<Page> pageLocale, IStringLocale<Panel> panelLocale)
            {
                public string Show()
                {
                    var page = new Holder(pageLocale);
                    var panel = new Holder(panelLocale);

                    return page.Locale["Page's"].Value;
                }
            }

            public sealed class Panel;
            """), "Page's");

        // Assert
        Assert.Equal("Page", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_name_only_approximate_types_through_a_field_written_again_after_construction()
    {
        // Act — a later write may replace what the constructor stored, so the constructor's value is not the one read.
        var site = Single(Resolve("""
            public sealed class Page
            {
                private IStringLocale _locale;

                public Page(IStringLocale<Page> locale) => _locale = locale;

                public void Replace(IStringLocale locale) => _locale = locale;

                public string Show() => _locale["Today"].Value;
            }
            """), "Today");

        // Assert
        Assert.Multiple(
            () => Assert.All(site.Origins, origin => Assert.True(origin.IsApproximate)),
            () => Assert.Contains(site.Gaps, gap => gap.Kind == LocaleGapKind.Write));
    }

    [Fact]
    public void Should_name_only_approximate_types_through_a_conditional_receiver()
    {
        // Act — the guard that pairs the receiver with the lookup is not followed, so neither type is certain.
        var site = Single(Resolve("""
            public sealed class Page(IStringLocale<Page> detailed, IStringLocale<Panel> compact, bool isCompact)
            {
                public string Show()
                {
                    var locale = isCompact ? (IStringLocale)compact : detailed;
                    if (isCompact)
                    {
                        return locale["CompactOnly"].Value;
                    }

                    return string.Empty;
                }
            }

            public sealed class Panel;
            """), "CompactOnly");

        // Assert
        Assert.All(site.Origins, origin => Assert.True(origin.IsApproximate));
    }

    [Fact]
    public void Should_name_the_type_the_factory_the_container_passes_a_constructor_no_code_calls_creates_a_locale_for()
    {
        // Act — nothing of the run creates the service, so the container does, passing the factory it registers.
        var site = Single(Resolve("""
            internal sealed class Texts(IStringLocaleFactory localeFactory)
            {
                private IStringLocale L { get; } = localeFactory.Create(typeof(Texts));

                public string Show() => L["Today"].Value;
            }
            """), "Today");

        // Assert
        Assert.Equal("Texts", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_name_no_type_through_a_constructor_a_librarys_consumers_may_call()
    {
        // Act — code outside the run may pass a factory of its own.
        var site = Single(Resolve("""
            public sealed class Texts(IStringLocaleFactory localeFactory)
            {
                private IStringLocale L { get; } = localeFactory.Create(typeof(Texts));

                public string Show() => L["Today"].Value;
            }
            """), "Today");

        // Assert
        Assert.Multiple(
            () => Assert.Empty(site.Origins),
            () => Assert.Contains(site.Gaps, gap => gap.Kind == LocaleGapKind.ExternalCallers));
    }

    [Fact]
    public void Should_name_the_type_the_registered_factory_creates_a_locale_for()
    {
        // Act
        var site = Single(Resolve("""
            public sealed class Page(IServiceProvider services)
            {
                public string Show() => services.GetStringLocaleFactory().Create(typeof(Page))["Today"].Value;
            }
            """), "Today");

        // Assert
        Assert.Equal("Page", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_name_no_type_through_a_factory_the_code_builds_itself()
    {
        // Act — a factory other than the registered one may read other files.
        var site = Single(Resolve("""
            internal sealed class Factory : IStringLocaleFactory
            {
                public IStringLocale Create(Type resourceSource) => throw new NotSupportedException();

                public IStringLocale Create(string baseName, string location) => throw new NotSupportedException();
            }

            public sealed class Page
            {
                public string Show() => new Factory().Create(typeof(Page))["Today"].Value;
            }
            """), "Today");

        // Assert
        Assert.Multiple(
            () => Assert.Empty(site.Origins),
            () => Assert.Equal(LocaleGapKind.CustomFactory, Assert.Single(site.Gaps).Kind));
    }

    [Fact]
    public void Should_name_each_components_type_through_a_context_factory_without_mixing_them()
    {
        // Act — every component's locale passes through one context factory, and each reads its own type.
        var sites = Resolve(ComponentModel + """
            public sealed class Widget : FormaLike
            {
                public string Show() => L["Widget's"].Value;
            }

            public sealed class Gizmo : FormaLike
            {
                protected override Type LocaleSourceType => typeof(Panel);

                public string Show() => L["Gizmo's"].Value;
            }

            public sealed class Panel;
            """);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Widget", Assert.Single(Single(sites, "Widget's").Origins).Type.Name),
            () => Assert.Equal("Panel", Assert.Single(Single(sites, "Gizmo's").Origins).Type.Name));
    }

    [Fact]
    public void Should_read_a_property_through_the_interface_an_object_implements()
    {
        // Act — the context is typed by its interface, as a component's is, and the object behind it is the one created.
        var site = Single(Resolve("""
            public interface IContext
            {
                IStringLocale Locale { get; }
            }

            public sealed class Context(IStringLocale locale) : IContext
            {
                public IStringLocale Locale => locale;
            }

            public sealed class Page(IStringLocale<Page> locale)
            {
                private IContext Context => new Context(locale);

                public string Show() => Context.Locale["Today"].Value;
            }
            """), "Today");

        // Assert
        Assert.Equal("Page", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_name_no_type_through_a_service_two_implementations_are_registered_for()
    {
        // Act — a host replacing the default leaves the implementation that runs unknown.
        var site = Single(Resolve(ComponentModel + """
            public sealed class OtherContextFactory : IContextFactory
            {
                public Context Create(Type componentType, Func<Type> localeSourceType) => throw new NotSupportedException();
            }

            public static class Host
            {
                public static void Replace(IServiceCollection services) => services.AddScoped<IContextFactory, OtherContextFactory>();
            }

            public sealed class Widget : FormaLike
            {
                public string Show() => L["Widget's"].Value;
            }
            """), "Widget's");

        // Assert
        Assert.Multiple(
            () => Assert.Empty(site.Origins),
            () => Assert.Contains(site.Gaps, gap => gap.Kind == LocaleGapKind.Dispatch));
    }

    [Fact]
    public void Should_leave_a_test_projects_registration_out_of_product_code()
    {
        // Arrange — a test registering a fake context factory speaks for the test alone.
        var product = CompilationTestFactory.Referenced(Usings + ComponentModel + """
            public sealed class Widget : FormaLike
            {
                public string Show() => L["Widget's"].Value;
            }
            """).WithAssemblyName("Contoso.Product");
        var tests = CompilationTestFactory.Referenced(Usings + """
            public sealed class FakeContextFactory : Scratch.IContextFactory
            {
                public Scratch.Context Create(Type componentType, Func<Type> localeSourceType) => throw new NotSupportedException();
            }

            public static class TestHost
            {
                public static void Register(IServiceCollection services) => services.AddScoped<Scratch.IContextFactory, FakeContextFactory>();
            }
            """).WithAssemblyName("Contoso.Product.Tests").AddReferences(product.ToMetadataReference());
        var project = new LoadedProject("Product.csproj", product, false, false);

        // Act
        var site = Single(LocaleFlowTestFactory.Resolve(project, [project, new LoadedProject("Tests.csproj", tests, false, false) { IsTestProject = true }]), "Widget's");

        // Assert
        Assert.Equal("Widget", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_name_no_type_for_a_test_projects_lookup_through_a_locale_factory_the_test_registers_itself()
    {
        // Arrange — the product's lookup is resolved first, so its answer for the shared member is there to be wrongly reused.
        var (product, tests) = TestedTexts(TestFactoryRegistration);

        // Act
        var resolved = LocaleFlowTestFactory.ResolveInTurn([product, tests]);

        // Assert
        var site = Single(resolved[1], "Bold");
        Assert.Multiple(
            () => Assert.Empty(site.Origins),
            () => Assert.Contains(site.Gaps, gap => gap.Kind == LocaleGapKind.CustomFactory));
    }

    [Fact]
    public void Should_keep_the_hosts_locale_factory_for_product_code_beside_a_test_registering_its_own()
    {
        // Arrange — the test's lookup is resolved first, so its answer for the shared member is there to be wrongly reused.
        var (product, tests) = TestedTexts(TestFactoryRegistration);

        // Act
        var resolved = LocaleFlowTestFactory.ResolveInTurn([tests, product]);

        // Assert
        Assert.Equal("Panel", Assert.Single(Single(resolved[1], "Today").Origins).Type.Name);
    }

    [Fact]
    public void Should_name_the_type_for_a_test_projects_lookup_through_the_hosts_locale_factory_where_the_test_registers_none()
    {
        // Arrange
        var (product, tests) = TestedTexts(string.Empty);

        // Act
        var resolved = LocaleFlowTestFactory.ResolveInTurn([product, tests]);

        // Assert
        Assert.Equal("Panel", Assert.Single(Single(resolved[1], "Bold").Origins).Type.Name);
    }

    [Fact]
    public void Should_name_no_type_for_a_key_a_test_writes_to_a_product_method_through_a_locale_factory_the_test_registers_itself()
    {
        // Arrange
        var (product, tests) = TestedTexts(TestFactoryRegistration + """

                public string Caption() => Scratch.Labels.Caption("Bold");
            """);

        // Act
        var forwarded = LocaleFlowTestFactory.Forward([product, tests]);

        // Assert
        var site = Assert.Single(forwarded.ByProject[tests.Path], lookup => lookup.Key.Text == "Bold");
        Assert.Multiple(
            () => Assert.Empty(site.Origins),
            () => Assert.Contains(site.Gaps, gap => gap.Kind == LocaleGapKind.CustomFactory));
    }

    [Fact]
    public void Should_name_no_type_for_a_test_projects_lookup_through_a_service_the_test_registers_another_implementation_of()
    {
        // Arrange
        var (product, tests) = TestedComponent("services.AddScoped<Scratch.IContextFactory, FakeContextFactory>()");

        // Act
        var site = Single(LocaleFlowTestFactory.ResolveInTurn([product, tests])[1], "Tested");

        // Assert
        Assert.Multiple(
            () => Assert.Empty(site.Origins),
            () => Assert.Contains(site.Gaps, gap => gap.Kind == LocaleGapKind.Dispatch));
    }

    [Fact]
    public void Should_name_the_type_for_a_test_projects_lookup_through_a_service_the_test_registers_as_product_code_does()
    {
        // Arrange
        var (product, tests) = TestedComponent("services.AddScoped<Scratch.IContextFactory, Scratch.ContextFactory>()");

        // Act
        var site = Single(LocaleFlowTestFactory.ResolveInTurn([product, tests])[1], "Tested");

        // Assert
        Assert.Equal("TestedWidget", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_name_the_type_a_Visual_Basic_helper_is_called_with_at_its_callers()
    {
        // Arrange
        var compilation = CompilationTestFactory.ReferencedVisualBasic("""
            Imports Forma.Framework.Localization

            Namespace Scratch
                Friend Module Labels
                    Function Folder(locale As IStringLocale) As String
                        Return locale("Folder").Value
                    End Function
                End Module

                Public NotInheritable Class Widget
                    Private ReadOnly Property L As IStringLocale(Of Widget)

                    Public Function Show() As String
                        Return Folder(L)
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var site = Single(LocaleFlowTestFactory.Resolve(compilation), "Folder");

        // Assert
        Assert.Equal("Widget", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_name_only_approximate_types_through_objects_a_conditional_chooses()
    {
        // Act — the guard that pairs the object with the lookup is not followed, so neither object's type is certain.
        var site = Single(Resolve("""
            internal sealed class Holder(IStringLocale locale)
            {
                public IStringLocale Locale => locale;
            }

            public sealed class Page(IStringLocale<Page> detailed, IStringLocale<Panel> compact, bool isCompact)
            {
                public string Show()
                {
                    var holder = isCompact ? new Holder(compact) : new Holder(detailed);

                    return isCompact ? holder.Locale["CompactOnly"].Value : string.Empty;
                }
            }

            public sealed class Panel;
            """), "CompactOnly");

        // Assert
        Assert.Multiple(
            () => Assert.Equal(2, site.Origins.Count),
            () => Assert.All(site.Origins, origin => Assert.True(origin.IsApproximate)));
    }

    [Fact]
    public void Should_name_only_approximate_types_through_delegates_a_conditional_chooses()
    {
        // Act
        var site = Single(Resolve("""
            public sealed class Page(IStringLocale<Page> detailed, IStringLocale<Panel> compact, bool isCompact)
            {
                public string Show()
                {
                    Func<IStringLocale> locale = isCompact ? () => compact : () => detailed;

                    return isCompact ? locale()["CompactOnly"].Value : string.Empty;
                }
            }

            public sealed class Panel;
            """), "CompactOnly");

        // Assert
        Assert.All(site.Origins, origin => Assert.True(origin.IsApproximate));
    }

    [Fact]
    public void Should_name_only_approximate_types_through_objects_a_method_returns_on_several_paths()
    {
        // Act
        var site = Single(Resolve("""
            internal sealed class Holder(IStringLocale locale)
            {
                public IStringLocale Locale => locale;
            }

            public sealed class Page(IStringLocale<Page> detailed, IStringLocale<Panel> compact, bool isCompact)
            {
                private Holder Pick()
                {
                    if (isCompact)
                    {
                        return new Holder(compact);
                    }

                    return new Holder(detailed);
                }

                public string Show() => isCompact ? Pick().Locale["CompactOnly"].Value : string.Empty;
            }

            public sealed class Panel;
            """), "CompactOnly");

        // Assert
        Assert.All(site.Origins, origin => Assert.True(origin.IsApproximate));
    }

    [Fact]
    public void Should_name_only_approximate_types_through_the_type_of_objects_a_conditional_chooses()
    {
        // Act
        var site = Single(Resolve("""
            public sealed class Page(IServiceProvider services, bool flag)
            {
                public string Show()
                    => services.GetStringLocaleFactory().Create((flag ? (object)new Page(services, flag) : new Panel()).GetType())["Today"].Value;
            }

            public sealed class Panel;
            """), "Today");

        // Assert
        Assert.All(site.Origins, origin => Assert.True(origin.IsApproximate));
    }

    [Fact]
    public void Should_name_no_type_through_a_field_a_static_method_writes_with_its_parameter()
    {
        // Act — the one write takes each caller's locale, so it pairs with no single object read.
        var site = Single(Resolve("""
            internal sealed class Holder
            {
                private IStringLocale? _locale;

                public static Holder For(IStringLocale locale)
                {
                    var holder = new Holder();
                    holder._locale = locale;

                    return holder;
                }

                public IStringLocale Locale => _locale!;
            }

            public sealed class Page(IStringLocale<Page> locale)
            {
                public string Show() => Holder.For(locale).Locale["Page's"].Value;
            }

            public sealed class Panel(IStringLocale<Panel> locale)
            {
                public string Show() => Holder.For(locale).Locale["Panel's"].Value;
            }
            """), "Page's");

        // Assert
        Assert.Multiple(
            () => Assert.Empty(site.Origins),
            () => Assert.Contains(site.Gaps, gap => gap.Kind == LocaleGapKind.Write));
    }

    [Fact]
    public void Should_name_no_type_through_a_field_a_method_writes_on_each_object_it_is_called_on()
    {
        // Act
        var site = Single(Resolve("""
            internal sealed class Holder
            {
                private IStringLocale? _locale;

                public void Use(IStringLocale locale) => _locale = locale;

                public IStringLocale Locale => _locale!;
            }

            public sealed class Page(IStringLocale<Page> pageLocale, IStringLocale<Panel> panelLocale)
            {
                public string Show()
                {
                    var page = new Holder();
                    page.Use(pageLocale);
                    var panel = new Holder();
                    panel.Use(panelLocale);

                    return page.Locale["Page's"].Value;
                }
            }

            public sealed class Panel;
            """), "Page's");

        // Assert
        Assert.Empty(site.Origins);
    }

    [Fact]
    public void Should_name_the_type_a_local_function_reads_from_the_frame_that_called_it()
    {
        // Act
        var sites = Resolve("""
            internal static class Locales
            {
                public static IStringLocale Wrap(IStringLocale locale)
                {
                    return Get();

                    IStringLocale Get() => locale;
                }
            }

            public sealed class Page(IStringLocale<Page> locale)
            {
                public string Show() => Locales.Wrap(locale)["Page's"].Value;
            }

            public sealed class Panel(IStringLocale<Panel> locale)
            {
                public string Show() => Locales.Wrap(locale)["Panel's"].Value;
            }
            """);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Page", Assert.Single(Single(sites, "Page's").Origins).Type.Name),
            () => Assert.Equal("Panel", Assert.Single(Single(sites, "Panel's").Origins).Type.Name));
    }

    [Fact]
    public void Should_name_only_approximate_types_through_a_static_field_a_lambda_in_an_initializer_writes()
    {
        // Act
        var site = Single(Resolve("""
            public static class Services
            {
                public static IServiceProvider Provider = null!;
            }

            public static class Texts
            {
                private static IStringLocale s_locale = Services.Provider.GetStringLocaleFactory().Create(typeof(Panel));

                private static readonly Action s_reset = () => s_locale = Services.Provider.GetStringLocaleFactory().Create(typeof(Page));

                public static string Show() => s_locale["Today"].Value;
            }

            public sealed class Page;

            public sealed class Panel;
            """), "Today");

        // Assert
        Assert.All(site.Origins, origin => Assert.True(origin.IsApproximate));
    }

    [Fact]
    public void Should_name_no_type_through_a_static_field_an_initializer_takes_by_reference()
    {
        // Act
        var site = Single(Resolve("""
            public static class Services
            {
                public static IServiceProvider Provider = null!;
            }

            public static class Texts
            {
                private static IStringLocale? s_locale;

                private static readonly bool s_set = System.Threading.Interlocked.Exchange(ref s_locale, null) is null;

                public static void Set() => s_locale = Services.Provider.GetStringLocaleFactory().Create(typeof(Page));

                public static string Show() => s_locale!["Today"].Value;
            }

            public sealed class Page;
            """), "Today");

        // Assert
        Assert.Empty(site.Origins);
    }

    [Fact]
    public void Should_name_no_type_through_a_service_registered_as_an_instance()
    {
        // Act — the instance may be of any implementation, so the registered default is not known to run.
        var site = Single(Resolve(ComponentModel + """
            public sealed class OtherContextFactory(IServiceProvider services) : IContextFactory
            {
                public Context Create(Type componentType, Func<Type> localeSourceType) => new(() => services.GetStringLocaleFactory().Create(typeof(Shared)));
            }

            public sealed class Shared;

            public static class Host
            {
                public static void Replace(IServiceCollection services, IServiceProvider provider)
                    => services.AddSingleton<IContextFactory>(Make(provider));

                private static IContextFactory Make(IServiceProvider provider) => new OtherContextFactory(provider);
            }

            public sealed class Widget : FormaLike
            {
                public string Show() => L["Widget's"].Value;
            }
            """), "Widget's");

        // Assert
        Assert.Multiple(
            () => Assert.Empty(site.Origins),
            () => Assert.Contains(site.Gaps, gap => gap.Kind == LocaleGapKind.Dispatch));
    }

    [Fact]
    public void Should_name_no_type_through_a_service_a_replacement_registers_again()
    {
        // Act
        var site = Single(Resolve(ComponentModel + """
            public sealed class OtherContextFactory(IServiceProvider services) : IContextFactory
            {
                public Context Create(Type componentType, Func<Type> localeSourceType) => new(() => services.GetStringLocaleFactory().Create(typeof(Shared)));
            }

            public sealed class Shared;

            public static class Host
            {
                public static void Replace(IServiceCollection services) => services.Replace(ServiceDescriptor.Scoped<IContextFactory, OtherContextFactory>());
            }

            public sealed class Widget : FormaLike
            {
                public string Show() => L["Widget's"].Value;
            }
            """), "Widget's");

        // Assert
        Assert.Empty(site.Origins);
    }

    [Fact]
    public void Should_name_no_type_through_a_service_a_factory_registers_without_creating_it()
    {
        // Act
        var site = Single(Resolve(ComponentModel + """
            public sealed class OtherContextFactory(IServiceProvider services) : IContextFactory
            {
                public Context Create(Type componentType, Func<Type> localeSourceType) => new(() => services.GetStringLocaleFactory().Create(typeof(Shared)));
            }

            public sealed class Shared;

            public static class Host
            {
                public static void Replace(IServiceCollection services)
                    => services.AddScoped<IContextFactory>(provider => provider.GetRequiredService<OtherContextFactory>());
            }

            public sealed class Widget : FormaLike
            {
                public string Show() => L["Widget's"].Value;
            }
            """), "Widget's");

        // Assert
        Assert.Empty(site.Origins);
    }

    [Fact]
    public void Should_name_no_type_through_a_service_a_Visual_Basic_project_registers_in_lowercase()
    {
        // Arrange — Visual Basic names a method in any case, so a second registration there still counts.
        var product = CompilationTestFactory.Referenced(Usings + ComponentModel + """
            public sealed class Widget : FormaLike
            {
                public string Show() => L["Widget's"].Value;
            }
            }
            """).WithAssemblyName("Contoso.Product");
        var host = CompilationTestFactory.ReferencedVisualBasic("""
            Imports System
            Imports Microsoft.Extensions.DependencyInjection

            Public NotInheritable Class OtherContextFactory
                Implements Scratch.IContextFactory

                Public Function Create(componentType As Type, localeSourceType As Func(Of Type)) As Scratch.Context Implements Scratch.IContextFactory.Create
                    Throw New NotSupportedException()
                End Function
            End Class

            Public Module Host
                Public Sub Register(services As IServiceCollection)
                    services.addscoped(Of Scratch.IContextFactory, OtherContextFactory)()
                End Sub
            End Module
            """).WithAssemblyName("Contoso.Host").AddReferences(CompilationTestFactory.MetadataOf(product));
        var project = new LoadedProject("Product.csproj", product, false, false);

        // Act
        var site = Single(LocaleFlowTestFactory.Resolve(project, [project, new LoadedProject("Host.vbproj", host, false, false)]), "Widget's");

        // Assert
        Assert.Empty(site.Origins);
    }

    [Fact]
    public void Should_name_no_type_through_a_service_a_descriptor_made_in_another_method_registers()
    {
        // Act — the descriptor names its service where it is created, wherever it is handed to the collection.
        var site = Single(Resolve(ComponentModel + """
            public sealed class OtherContextFactory : IContextFactory
            {
                public Context Create(Type componentType, Func<Type> localeSourceType) => throw new NotSupportedException();
            }

            public static class Host
            {
                public static void Replace(IServiceCollection services) => services.Add(Make());

                private static ServiceDescriptor Make() => new(typeof(IContextFactory), typeof(OtherContextFactory), ServiceLifetime.Scoped);
            }

            public sealed class Widget : FormaLike
            {
                public string Show() => L["Widget's"].Value;
            }
            """), "Widget's");

        // Assert
        Assert.Empty(site.Origins);
    }

    [Fact]
    public void Should_name_no_type_through_a_service_a_helper_handed_the_collection_registers()
    {
        // Act — a helper of the application's own registers the service it is given however it likes.
        var site = Single(Resolve(ComponentModel + """
            public static class Host
            {
                public static void Replace(IServiceCollection services) => Register<IContextFactory>(services);

                private static void Register<TService>(IServiceCollection services)
                    where TService : class
                    => throw new NotSupportedException();
            }

            public sealed class Widget : FormaLike
            {
                public string Show() => L["Widget's"].Value;
            }
            """), "Widget's");

        // Assert
        Assert.Empty(site.Origins);
    }

    [Fact]
    public void Should_name_the_type_the_registration_of_an_injected_class_names()
    {
        // Act — the class asked for is a service, and the registration chooses the class that answers it.
        var site = Single(Resolve("""
            namespace Microsoft.AspNetCore.Components
            {
                [AttributeUsage(AttributeTargets.Property)]
                public sealed class InjectAttribute : Attribute;
            }

            namespace Scratch
            {
                public class Texts(IServiceProvider services)
                {
                    public virtual IStringLocale L => services.GetStringLocaleFactory().Create(typeof(Texts));
                }

                public sealed class AppTexts(IServiceProvider services) : Texts(services)
                {
                    public override IStringLocale L => services.GetStringLocaleFactory().Create(typeof(AppTexts));
                }

                public static class Host
                {
                    public static void Register(IServiceCollection services) => services.AddScoped<Texts, AppTexts>();
                }

                public sealed class Page
                {
                    [Microsoft.AspNetCore.Components.Inject]
                    private Texts Texts { get; set; } = default!;

                    public string Show() => Texts.L["Today"].Value;
                }
            """), "Today");

        // Assert
        Assert.Equal("AppTexts", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_follow_a_method_called_within_its_own_argument()
    {
        // Act
        var site = Single(Resolve("""
            internal static class Locales
            {
                public static IStringLocale Same(IStringLocale locale) => locale;
            }

            public sealed class Page(IStringLocale<Page> locale)
            {
                public string Show() => Locales.Same(Locales.Same(locale))["Today"].Value;
            }
            """), "Today");

        // Assert
        Assert.Equal("Page", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_end_a_path_through_a_method_recursing_on_its_own_arguments()
    {
        // Act — each call recurses twice, so the path ends once its calls repeat rather than at the depth limit.
        var site = Single(Resolve("""
            internal static class Locales
            {
                public static IStringLocale Pick(IStringLocale locale, int count, bool flag)
                    => count == 0 ? locale : flag ? Pick(locale, count - 1, !flag) : Pick(locale, count - 1, flag);
            }

            public sealed class Page(IStringLocale<Page> locale)
            {
                public string Show() => Locales.Pick(locale, 3, true)["Today"].Value;
            }
            """), "Today");

        // Assert
        Assert.All(site.Origins, origin => Assert.Equal("Page", origin.Type.Name));
    }

    [Fact]
    public void Should_name_no_type_through_a_local_a_deconstruction_writes()
    {
        // Act
        var site = Single(Resolve("""
            public sealed class Page(IStringLocale<Page> pageLocale, IStringLocale<Panel> panelLocale)
            {
                public string Show()
                {
                    IStringLocale locale = pageLocale;
                    (locale, _) = (panelLocale, 0);

                    return locale["Today"].Value;
                }
            }

            public sealed class Panel;
            """), "Today");

        // Assert
        Assert.Empty(site.Origins);
    }

    [Fact]
    public void Should_name_only_approximate_types_through_a_field_a_constructor_writes_over_its_initializer()
    {
        // Act
        var site = Single(Resolve("""
            public static class Services
            {
                public static IServiceProvider Provider = null!;
            }

            public sealed class Page
            {
                private readonly IStringLocale _locale = Services.Provider.GetStringLocaleFactory().Create(typeof(Panel));

                public Page(IServiceProvider services) => _locale = services.GetStringLocaleFactory().Create(typeof(Page));

                public string Show() => _locale["Today"].Value;
            }

            public sealed class Panel;
            """), "Today");

        // Assert
        Assert.All(site.Origins, origin => Assert.True(origin.IsApproximate));
    }

    [Fact]
    public void Should_name_the_type_a_derived_primary_constructor_passes_to_its_base()
    {
        // Act
        var sites = Resolve("""
            internal class Holder(IStringLocale locale)
            {
                public IStringLocale Locale => locale;
            }

            internal sealed class PageHolder(IStringLocale<Page> locale) : Holder(locale);

            internal sealed class PanelHolder(IStringLocale<Panel> locale) : Holder(locale);

            public sealed class Page(IStringLocale<Page> locale)
            {
                public string Show() => new PageHolder(locale).Locale["Page's"].Value;
            }

            public sealed class Panel(IStringLocale<Panel> locale)
            {
                public string Show() => new PanelHolder(locale).Locale["Panel's"].Value;
            }
            """);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Page", Assert.Single(Single(sites, "Page's").Origins).Type.Name),
            () => Assert.Equal("Panel", Assert.Single(Single(sites, "Panel's").Origins).Type.Name));
    }

    [Fact]
    public void Should_name_the_type_a_chained_constructor_passes_to_the_primary_one()
    {
        // Act
        var site = Single(Resolve("""
            internal sealed class Holder(IStringLocale locale)
            {
                public Holder(IStringLocale<Page> page, int unused)
                    : this((IStringLocale)page)
                {
                }

                public IStringLocale Locale => locale;
            }

            public sealed class Page(IStringLocale<Page> locale, IStringLocale<Panel> panel)
            {
                public string Show()
                {
                    var other = new Holder(panel);

                    return new Holder(locale, 0).Locale["Page's"].Value;
                }
            }

            public sealed class Panel;
            """), "Page's");

        // Assert
        Assert.Equal("Page", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_name_the_derived_type_a_covariant_locale_holds()
    {
        // Act — the contract is covariant, so a locale for a derived type may stand where one for its base is read.
        var site = Single(Resolve("""
            public class Base;

            public sealed class Derived : Base;

            public sealed class Page(IStringLocale<Derived> locale)
            {
                private IStringLocale<Base> L => locale;

                public string Show() => L["Today"].Value;
            }
            """), "Today");

        // Assert
        Assert.Equal("Derived", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_name_the_declared_type_of_a_covariant_locale_no_code_of_the_run_passes()
    {
        // Act — what creates the component, a container among them, keeps the contract the parameter declares.
        var site = Single(Resolve("""
            public class Base;

            public sealed class Derived : Base;

            public sealed class Page(IStringLocale<Base> locale)
            {
                public string Show() => locale["Today"].Value;
            }
            """), "Today");

        // Assert
        Assert.Equal("Base", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_name_the_base_type_a_base_qualified_property_reads()
    {
        // Act
        var site = Single(Resolve("""
            public class Base(IServiceProvider services)
            {
                public virtual IStringLocale Locale => services.GetStringLocaleFactory().Create(typeof(Base));
            }

            public sealed class Derived(IServiceProvider services) : Base(services)
            {
                public override IStringLocale Locale => services.GetStringLocaleFactory().Create(typeof(Derived));

                public string Show() => base.Locale["BaseKey"].Value;
            }
            """), "BaseKey");

        // Assert
        Assert.Equal("Base", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_name_the_base_type_a_base_qualified_call_reads()
    {
        // Act
        var site = Single(Resolve("""
            public class Base(IServiceProvider services)
            {
                protected virtual IStringLocale Make() => services.GetStringLocaleFactory().Create(typeof(Base));
            }

            public sealed class Derived(IServiceProvider services) : Base(services)
            {
                protected override IStringLocale Make() => services.GetStringLocaleFactory().Create(typeof(Derived));

                public string Show() => base.Make()["BaseKey"].Value;
            }
            """), "BaseKey");

        // Assert
        Assert.Equal("Base", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_keep_a_named_property_whose_path_is_cut_short_apart_from_one_that_names_no_type()
    {
        // Act — a getter whose path ends in a cycle may name a type the flow did not reach, so it is not one that names none.
        var site = Assert.Single(FindThroughComponent("""
            public sealed class Widget : Component
            {
                protected override Type LocalizationSourceType => Pick();

                private Type Pick() => Pick();

                public void Show() => _ = L["Today"];
            }
            """));

        // Assert
        Assert.Multiple(
            () => Assert.Empty(site.Origins),
            () => Assert.Contains(site.Gaps, gap => gap.Kind == LocaleGapKind.Cycle),
            () => Assert.DoesNotContain(site.Gaps, gap => gap.Kind == LocaleGapKind.UnreadTypeMember));
    }

    [Fact]
    public void Should_name_no_type_through_a_field_written_on_another_object()
    {
        // Act — the value is the writer's, and the object read may be any other.
        var site = Single(Resolve("""
            public abstract class Holder(IServiceProvider services)
            {
                private IStringLocale? _locale;

                public void Give(Holder other) => other._locale = services.GetStringLocaleFactory().Create(GetType());

                public string Show() => _locale!["Today"].Value;
            }

            public sealed class Page(IServiceProvider services) : Holder(services);

            public sealed class Panel(IServiceProvider services) : Holder(services);
            """), "Today");

        // Assert
        Assert.Empty(site.Origins);
    }

    [Fact]
    public void Should_name_only_approximate_types_through_a_static_field_its_one_write_takes_from_each_caller()
    {
        // Act — a static field holds one caller's locale at a time.
        var site = Single(Resolve("""
            public static class Texts
            {
                private static IStringLocale? s_locale;

                public static void Use(IStringLocale locale) => s_locale = locale;

                public static string Show() => s_locale!["Today"].Value;
            }

            public sealed class Page(IStringLocale<Page> locale)
            {
                public void Start() => Texts.Use(locale);
            }

            public sealed class Panel(IStringLocale<Panel> locale)
            {
                public void Start() => Texts.Use(locale);
            }
            """), "Today");

        // Assert
        Assert.Multiple(
            () => Assert.Equal(2, site.Origins.Count),
            () => Assert.All(site.Origins, origin => Assert.True(origin.IsApproximate)));
    }

    [Fact]
    public void Should_name_the_type_a_generic_locale_declares_in_an_abstract_type_no_instance_has()
    {
        // Act
        var site = Single(Resolve("""
            public abstract class Widget(IStringLocale<Widget> locale)
            {
                public string Show() => locale["Today"].Value;
            }
            """), "Today");

        // Assert
        Assert.Equal("Widget", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_name_no_type_through_a_service_a_descriptor_created_without_its_name_registers()
    {
        // Act
        var site = Single(Resolve(ComponentModel + """
            public sealed class OtherContextFactory : IContextFactory
            {
                public Context Create(Type componentType, Func<Type> localeSourceType) => throw new NotSupportedException();
            }

            public static class Host
            {
                public static void Replace(IServiceCollection services)
                    => services.Add(new(typeof(IContextFactory), typeof(OtherContextFactory), ServiceLifetime.Scoped));
            }

            public sealed class Widget : FormaLike
            {
                public string Show() => L["Widget's"].Value;
            }
            """), "Widget's");

        // Assert
        Assert.Empty(site.Origins);
    }

    [Fact]
    public void Should_rest_a_type_only_a_hint_supplies_on_that_hint()
    {
        // Act — the component's locale is opaque to the flow, so the type is the hint's alone.
        var site = Assert.Single(FindThroughComponent("""
            public sealed class Widget : Component
            {
                public void Show() => _ = L["Today"];
            }
            """));

        // Assert
        var origin = Assert.Single(site.Origins);
        Assert.Multiple(
            () => Assert.Equal("Widget", origin.Type.Name),
            () => Assert.Equal(LocaleEvidence.Hint, origin.Evidence),
            () => Assert.Equal(ComponentHint, origin.Hint));
    }

    [Fact]
    public void Should_verify_a_hint_the_flow_reaches_the_same_type_for()
    {
        // Act
        var (sites, diagnostics) = LocaleFlowTestFactory.Run([LocaleFlowTestFactory.Hint("Scratch", "Scratch.FormaLike", typeMember: "LocaleSourceType")], CompilationTestFactory.Referenced(Usings + ComponentModel + """
            public sealed class Widget : FormaLike
            {
                public string Show() => L["Widget's"].Value;
            }
            }
            """));

        // Assert
        var origin = Assert.Single(Single(sites, "Widget's").Origins);
        Assert.Multiple(
            () => Assert.Equal("Widget", origin.Type.Name),
            () => Assert.Equal(LocaleEvidence.VerifiedHint, origin.Evidence),
            () => Assert.Empty(diagnostics));
    }

    [Fact]
    public void Should_report_a_hint_the_flow_contradicts_and_check_the_flows_type()
    {
        // Act — the member reads a fixed type, while the hint relates it to the instance's own.
        var (sites, diagnostics) = LocaleFlowTestFactory.Run([ComponentHint], CompilationTestFactory.Referenced(Usings + """
            namespace Scratch;

            public abstract class Component(IServiceProvider services)
            {
                protected virtual Type LocalizationSourceType => GetType();

                protected IStringLocale L => services.GetStringLocaleFactory().Create(typeof(Panel));
            }

            public sealed class Panel;

            public sealed class Widget(IServiceProvider services) : Component(services)
            {
                public string Show() => L["Today"].Value;
            }
            """));

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        var origin = Assert.Single(Single(sites, "Today").Origins);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.ConflictingHint, diagnostic.Code),
            () => Assert.Equal(
                "The hint relating 'Scratch.Component.L' to 'LocalizationSourceType' reads 'Widget' for 'Widget', where the flow reaches 'Panel', so the flow's type is checked.",
                diagnostic.Message),
            () => Assert.Equal("Panel", origin.Type.Name),
            () => Assert.Equal(LocaleEvidence.Analysis, origin.Evidence));
    }

    [Fact]
    public void Should_leave_a_member_overriding_a_hinted_one_to_the_flow()
    {
        // Act — the hint is written for the base's declaration, which the override replaces.
        var (sites, diagnostics) = LocaleFlowTestFactory.Run([ComponentHint], CompilationTestFactory.Referenced(Usings + """
            namespace Scratch;

            public abstract class Component
            {
                protected virtual Type LocalizationSourceType => GetType();

                protected virtual IStringLocale L => default!;

                public string Show() => L["Today"].Value;
            }

            public sealed class Panel;

            public sealed class Widget(IServiceProvider services) : Component
            {
                protected override IStringLocale L => services.GetStringLocaleFactory().Create(typeof(Panel));

                public string Own() => L["Own"].Value;
            }
            """));

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Panel", Assert.Single(Single(sites, "Today").Origins).Type.Name),
            () => Assert.Equal("Panel", Assert.Single(Single(sites, "Own").Origins).Type.Name),
            () => Assert.Empty(diagnostics));
    }

    [Fact]
    public void Should_not_let_two_hints_verify_one_another()
    {
        // Act — each member reads the other, so only the hints say what either reads.
        var site = Assert.Single(LocaleFlowTestFactory.Resolve(
            [ComponentHint, LocaleFlowTestFactory.Hint("Scratch", "Scratch.Component", member: "M")],
            CompilationTestFactory.Referenced(Usings + """
                namespace Scratch;

                public abstract class Component
                {
                    protected virtual Type LocalizationSourceType => GetType();

                    protected IStringLocale L => M;

                    protected IStringLocale M => L;
                }

                public sealed class Widget : Component
                {
                    public void Show() => _ = L["Today"];
                }
                """)));

        // Assert
        Assert.Equal(LocaleEvidence.Hint, Assert.Single(site.Origins).Evidence);
    }

    [Fact]
    public void Should_report_a_hint_naming_a_type_its_assembly_does_not_declare()
    {
        // Act
        var (_, diagnostics) = LocaleFlowTestFactory.Run([LocaleFlowTestFactory.Hint("Scratch", "Scratch.Missing")], ComponentCompilation());

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.InvalidHint, diagnostic.Code),
            () => Assert.Equal(LintingSeverity.Error, diagnostic.Severity),
            () => Assert.Equal("'Scratch.Missing' is not a type 'Scratch' declares, so the hint relates nothing.", diagnostic.Message));
    }

    [Fact]
    public void Should_report_a_hint_naming_a_member_that_returns_no_locale()
    {
        // Act
        var (_, diagnostics) = LocaleFlowTestFactory.Run([LocaleFlowTestFactory.Hint("Scratch", "Scratch.Component", member: "LocalizationSourceType")], ComponentCompilation());

        // Assert
        Assert.Equal(LintingCodes.InvalidHint, Assert.Single(diagnostics).Code);
    }

    [Fact]
    public void Should_report_a_hint_naming_an_abstract_member()
    {
        // Act — every instance reads an override, a declaration of its own.
        var (_, diagnostics) = LocaleFlowTestFactory.Run([ComponentHint], CompilationTestFactory.Referenced(Usings + """
            namespace Scratch;

            public abstract class Component
            {
                protected virtual Type LocalizationSourceType => GetType();

                protected abstract IStringLocale L { get; }
            }

            public sealed class Widget : Component
            {
                protected override IStringLocale L => default!;

                public void Show() => _ = L["Today"];
            }
            """));

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.InvalidHint, diagnostic.Code),
            () => Assert.Equal("'L' of 'Scratch.Component' is abstract, so an instance reads an override the hint was not written for, and the hint relates nothing.", diagnostic.Message));
    }

    [Fact]
    public void Should_report_a_hint_naming_a_type_member_that_returns_no_type()
    {
        // Act
        var (_, diagnostics) = LocaleFlowTestFactory.Run([LocaleFlowTestFactory.Hint("Scratch", "Scratch.Component", typeMember: "L")], ComponentCompilation());

        // Assert
        Assert.Equal(LintingCodes.InvalidHint, Assert.Single(diagnostics).Code);
    }

    [Fact]
    public void Should_report_a_hint_naming_an_assembly_the_run_neither_builds_nor_references_as_applying_to_nothing()
    {
        // Act
        var (_, diagnostics) = LocaleFlowTestFactory.Run([LocaleFlowTestFactory.Hint("Contoso.Missing", "Contoso.Missing.Component")], ComponentCompilation());

        // Assert
        Assert.Equal(LintingCodes.StaleConfigurationEntry, Assert.Single(diagnostics).Code);
    }

    [Fact]
    public void Should_report_two_hints_relating_one_member_to_different_type_members_and_apply_neither()
    {
        // Act
        var (sites, diagnostics) = LocaleFlowTestFactory.Run(
            [ComponentHint, LocaleFlowTestFactory.Hint("Scratch", "Scratch.Component", typeMember: "OtherType", line: 2)],
            ComponentCompilation("protected Type OtherType => typeof(Widget);"));

        // Assert
        Assert.Multiple(
            () => Assert.Equal([LintingCodes.ConflictingHint, LintingCodes.ConflictingHint], diagnostics.Select(diagnostic => diagnostic.Code)),
            () => Assert.Empty(Assert.Single(sites).Origins));
    }

    [Fact]
    public void Should_apply_one_hint_written_twice()
    {
        // Act — a package's hints and the application's own may both relate the member, alike.
        var (sites, diagnostics) = LocaleFlowTestFactory.Run([ComponentHint, LocaleFlowTestFactory.Hint("Scratch", "Scratch.Component", line: 2)], ComponentCompilation());

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Widget", Assert.Single(Assert.Single(sites).Origins).Type.Name),
            () => Assert.Empty(diagnostics));
    }

    [Fact]
    public void Should_hold_a_lookup_through_Microsofts_localizer_to_its_type_where_Formas_adapter_answers_it()
    {
        // Act
        var site = Single(Resolve(StringLocalizerFakeData.Declarations + """
            namespace Scratch
            {
                public static class Host
                {
                    public static void Register(IServiceCollection services) => Forma.Framework.Localization.StringLocalizerAdapter.ServiceCollectionExtensions.AddFormaStringLocalizerAdapter(services);
                }

                public sealed class Checkout(Microsoft.Extensions.Localization.IStringLocalizer<Checkout> localizer)
                {
                    public string Title => localizer["Checkout"].Value;
                }
            """), "Checkout");

        // Assert
        Assert.Multiple(
            () => Assert.True(site.UsesStringLocalizer),
            () => Assert.Equal("Checkout", Assert.Single(site.Origins).Type.Name));
    }

    [Fact]
    public void Should_leave_out_a_lookup_through_Microsofts_localizer_where_the_run_never_registers_Formas_adapter()
    {
        // Act — Microsoft's localizer reads its own resources unless the adapter answers it.
        var sites = Resolve(StringLocalizerFakeData.Declarations + """
            namespace Scratch
            {
                public sealed class Checkout(Microsoft.Extensions.Localization.IStringLocalizer<Checkout> localizer)
                {
                    public string Title => localizer["Checkout"].Value;
                }
            """);

        // Assert
        Assert.DoesNotContain(sites, site => site.Key.Text == "Checkout");
    }

    [Fact]
    public void Should_leave_out_a_lookup_through_Microsofts_localizer_where_Microsofts_localization_is_registered_first()
    {
        // Act — the adapter registers only where nothing is registered yet, so Microsoft's localization registered first answers.
        var sites = Resolve(StringLocalizerFakeData.Declarations + """
            namespace Scratch
            {
                public static class Host
                {
                    public static void Register(IServiceCollection services)
                    {
                        services.AddLocalization();
                        Forma.Framework.Localization.StringLocalizerAdapter.ServiceCollectionExtensions.AddFormaStringLocalizerAdapter(services);
                    }
                }

                public sealed class Checkout(Microsoft.Extensions.Localization.IStringLocalizer<Checkout> localizer)
                {
                    public string Title => localizer["Checkout"].Value;
                }
            """);

        // Assert
        Assert.DoesNotContain(sites, site => site.Key.Text == "Checkout");
    }

    [Fact]
    public void Should_leave_out_a_lookup_through_Microsofts_localizer_where_view_localization_registers_Microsofts_first()
    {
        // Act — view localization registers Microsoft's localization of its own.
        var sites = Resolve(StringLocalizerFakeData.Declarations + """
            namespace Scratch
            {
                public static class Host
                {
                    public static void Register(IServiceCollection services)
                    {
                        services.AddViewLocalization();
                        Forma.Framework.Localization.StringLocalizerAdapter.ServiceCollectionExtensions.AddFormaStringLocalizerAdapter(services);
                    }
                }

                public sealed class Checkout(Microsoft.Extensions.Localization.IStringLocalizer<Checkout> localizer)
                {
                    public string Title => localizer["Checkout"].Value;
                }
            """);

        // Assert
        Assert.DoesNotContain(sites, site => site.Key.Text == "Checkout");
    }

    [Fact]
    public void Should_hold_a_lookup_through_Microsofts_localizer_to_its_type_where_Formas_adapter_registers_before_Microsofts_localization()
    {
        // Act — Microsoft's localization registers only where nothing is registered yet, so the adapter registered first answers.
        var site = Single(Resolve(StringLocalizerFakeData.Declarations + """
            namespace Scratch
            {
                public static class Host
                {
                    public static void Register(IServiceCollection services)
                    {
                        Forma.Framework.Localization.StringLocalizerAdapter.ServiceCollectionExtensions.AddFormaStringLocalizerAdapter(services);
                        services.AddViewLocalization();
                    }
                }

                public sealed class Checkout(Microsoft.Extensions.Localization.IStringLocalizer<Checkout> localizer)
                {
                    public string Title => localizer["Checkout"].Value;
                }
            """), "Checkout");

        // Assert
        Assert.Equal("Checkout", Assert.Single(site.Origins).Type.Name);
    }

    [Fact]
    public void Should_leave_out_a_lookup_through_Microsofts_localizer_where_the_application_registers_its_own_factory()
    {
        // Act
        var sites = Resolve(StringLocalizerFakeData.Declarations + """
            namespace Scratch
            {
                public sealed class Factory : Microsoft.Extensions.Localization.IStringLocalizerFactory
                {
                    public Microsoft.Extensions.Localization.IStringLocalizer Create(Type resourceSource) => throw new NotSupportedException();
                }

                public static class Host
                {
                    public static void Register(IServiceCollection services)
                    {
                        services.AddSingleton<Microsoft.Extensions.Localization.IStringLocalizerFactory, Factory>();
                        Forma.Framework.Localization.StringLocalizerAdapter.ServiceCollectionExtensions.AddFormaStringLocalizerAdapter(services);
                    }
                }

                public sealed class Checkout(Microsoft.Extensions.Localization.IStringLocalizer<Checkout> localizer)
                {
                    public string Title => localizer["Checkout"].Value;
                }
            """);

        // Assert
        Assert.DoesNotContain(sites, site => site.Key.Text == "Checkout");
    }

    [Fact]
    public void Should_check_the_key_each_call_writes_where_a_lookup_takes_it_from_a_parameter()
    {
        // Act
        var forwarded = Forward("""
            public sealed class Page(IStringLocale<Page> locale)
            {
                private string Label(string key) => locale[key].Value;

                public string Show() => Label("Today");
            }
            """);

        // Assert
        var site = Assert.Single(Assert.Single(forwarded.ByProject).Value);
        Assert.Multiple(
            () => Assert.Equal("Today", site.Key.Text),
            () => Assert.Equal("Page", Assert.Single(site.Origins).Type.Name),
            () => Assert.Equal(LocalizerArguments.Unknown, site.Arguments),
            () => Assert.Empty(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_hold_each_calls_key_to_the_type_that_call_binds()
    {
        // Act — two calls pass different types to one method, and neither's key is read in the other's.
        var forwarded = Forward("""
            internal static class Labels
            {
                public static LocalizableText Label<TSource>(string key) => LocalizableText.For<TSource>(key);
            }

            public sealed class Page
            {
                public LocalizableText Show() => Labels.Label<Page>("Page's");
            }

            public sealed class Panel
            {
                public LocalizableText Show() => Labels.Label<Panel>("Panel's");
            }
            """);

        // Assert
        var sites = Assert.Single(forwarded.ByProject).Value;
        Assert.Multiple(
            () => Assert.Equal("Page", Assert.Single(Single(sites, "Page's").Origins).Type.Name),
            () => Assert.Equal("Panel", Assert.Single(Single(sites, "Panel's").Origins).Type.Name));
    }

    [Fact]
    public void Should_follow_a_key_a_method_passes_on_to_another()
    {
        // Act
        var forwarded = Forward("""
            internal static class Labels
            {
                public static LocalizableText Outer<TSource>(string key) => Inner<TSource>(key);

                private static LocalizableText Inner<TSource>(string key) => LocalizableText.For<TSource>(key);
            }

            public sealed class Page
            {
                public LocalizableText Show() => Labels.Outer<Page>("Today");
            }
            """);

        // Assert
        var site = Assert.Single(Assert.Single(forwarded.ByProject).Value);
        Assert.Multiple(
            () => Assert.Equal("Today", site.Key.Text),
            () => Assert.Equal("Page", Assert.Single(site.Origins).Type.Name),
            () => Assert.Empty(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_leave_a_key_unread_where_a_call_writes_it_as_an_expression()
    {
        // Act — the call writing text is still checked.
        var forwarded = Forward("""
            public sealed class Page(IStringLocale<Page> locale)
            {
                private string Label(string key) => locale[key].Value;

                public string Show(object column) => Label("Today") + Label(column.ToString()!);
            }
            """);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Today", Assert.Single(Assert.Single(forwarded.ByProject).Value).Key.Text),
            () => Assert.Equal("key", Assert.Single(forwarded.Unforwarded).Parameter));
    }

    [Fact]
    public void Should_leave_unread_a_key_no_call_of_the_run_writes()
    {
        // Act
        var forwarded = Forward("""
            public sealed class Page(IStringLocale<Page> locale)
            {
                private string Label(string key) => locale[key].Value;
            }
            """);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(forwarded.ByProject),
            () => Assert.Single(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_leave_unread_a_key_a_librarys_consumers_may_write()
    {
        // Act — the run's own call is checked, while calls outside it write keys it never sees.
        var forwarded = Forward("""
            public static class Labels
            {
                public static LocalizableText Label<TSource>(string key) => LocalizableText.For<TSource>(key);
            }

            internal sealed class Page
            {
                public LocalizableText Show() => Labels.Label<Page>("Today");
            }
            """);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Today", Assert.Single(Assert.Single(forwarded.ByProject).Value).Key.Text),
            () => Assert.Single(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_leave_out_a_call_whose_text_a_condition_keeps_from_the_lookup()
    {
        // Act — an empty key is never looked up, so the call writing it reads nothing, while the other's text is.
        var forwarded = Forward("""
            public sealed class Page(IStringLocale<Page> locale)
            {
                private string Text(string key) => string.IsNullOrEmpty(key) ? string.Empty : locale[key].Value;

                public string Show() => Text("") + Text("Today");
            }
            """);

        // Assert
        var site = Assert.Single(Assert.Single(forwarded.ByProject).Value);
        Assert.Multiple(
            () => Assert.Equal("Today", site.Key.Text),
            () => Assert.Empty(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_leave_unread_a_call_whose_text_a_condition_the_flow_cannot_settle_may_keep_from_the_lookup()
    {
        // Act — the method tells raw text from a key by a test on the text no rule reads, so the call's text may be no key.
        var forwarded = Forward("""
            public sealed class Page(IStringLocale<Page> locale)
            {
                private string Text(string key) => key.StartsWith("raw:") ? key[4..] : locale[key].Value;

                public string Show() => Text("raw:Today");
            }
            """);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(forwarded.ByProject),
            () => Assert.Equal("key", Assert.Single(forwarded.Unforwarded).Parameter));
    }

    [Fact]
    public void Should_settle_an_ordinal_test_of_the_keys_start_by_each_calls_text()
    {
        // Act — the raw text is returned as it is, and the other call's text is looked up.
        var forwarded = Forward("""
            public sealed class Page(IStringLocale<Page> locale)
            {
                private string Text(string key) => key.StartsWith("raw:", StringComparison.Ordinal) ? key[4..] : locale[key].Value;

                public string Show() => Text("raw:Today") + Text("Today");
            }
            """);

        // Assert
        var site = Assert.Single(Assert.Single(forwarded.ByProject).Value);
        Assert.Multiple(
            () => Assert.Equal("Today", site.Key.Text),
            () => Assert.Empty(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_leave_unread_a_call_whose_text_a_comparison_through_a_conversion_the_flow_does_not_make_may_keep_from_the_lookup()
    {
        // Act — the four-letter text equals 4.0 once its length is a double, so it is never looked up.
        var forwarded = Forward("""
            public sealed class Page(IStringLocale<Page> locale)
            {
                private string Text(string key) => key.Length == 4.0 ? key : locale[key].Value;

                public string Show() => Text("Raw!");
            }
            """);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(forwarded.ByProject),
            () => Assert.Single(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_check_a_calls_text_under_a_condition_on_something_other_than_the_key()
    {
        // Act — the condition is the object's, not the call's, so the text reaches the lookup as a key written under it would.
        var forwarded = Forward("""
            public sealed class Page(IStringLocale<Page> locale, bool showKeys)
            {
                private string Text(string key) => showKeys ? key : locale[key].Value;

                public string Show() => Text("Today");
            }
            """);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Today", Assert.Single(Assert.Single(forwarded.ByProject).Value).Key.Text),
            () => Assert.Empty(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_settle_a_condition_on_another_parameter_by_what_each_call_passes()
    {
        // Act — the call passing false never looks its text up, and the call passing true does.
        var forwarded = Forward("""
            public sealed class Page(IStringLocale<Page> locale)
            {
                private string Text(string text, bool localize) => localize ? locale[text].Value : text;

                public string Show() => Text("Raw text", false) + Text("Today", true);
            }
            """);

        // Assert
        var site = Assert.Single(Assert.Single(forwarded.ByProject).Value);
        Assert.Multiple(
            () => Assert.Equal("Today", site.Key.Text),
            () => Assert.Empty(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_leave_complete_a_key_a_call_writes_as_an_expression_where_that_call_never_reaches_the_lookup()
    {
        // Act — the expression is passed with false, so it is never looked up.
        var forwarded = Forward("""
            public sealed class Page(IStringLocale<Page> locale)
            {
                private string Text(string text, bool localize) => localize ? locale[text].Value : text;

                public string Show(object column) => Text(column.ToString()!, false) + Text("Today", true);
            }
            """);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Today", Assert.Single(Assert.Single(forwarded.ByProject).Value).Key.Text),
            () => Assert.Empty(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_carry_what_a_call_passes_through_a_method_passing_the_key_on()
    {
        // Act — the outer method passes the key on with a constant the inner one settles its condition by.
        var forwarded = Forward("""
            public sealed class Page(IStringLocale<Page> locale)
            {
                private string Raw(string text) => Text(text, false);

                private string Localized(string text) => Text(text, true);

                private string Text(string text, bool localize) => localize ? locale[text].Value : text;

                public string Show() => Raw("Raw text") + Localized("Today");
            }
            """);

        // Assert
        var site = Assert.Single(Assert.Single(forwarded.ByProject).Value);
        Assert.Multiple(
            () => Assert.Equal("Today", site.Key.Text),
            () => Assert.Empty(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_check_a_calls_text_where_the_key_is_written_only_on_a_path_that_call_does_not_take()
    {
        // Act — the fallback is written only where the key is null, which this call's text is not.
        var forwarded = Forward("""
            public sealed class Page(IStringLocale<Page> locale)
            {
                private string Text(string? key)
                {
                    key ??= "Fallback";

                    return locale[key].Value;
                }

                public string Show() => Text("Today");
            }
            """);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Today", Assert.Single(Assert.Single(forwarded.ByProject).Value).Key.Text),
            () => Assert.Empty(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_leave_unread_a_call_whose_text_the_lookup_reads_only_where_reading_it_throws()
    {
        // Act — the lookup runs only where parsing the text fails, which the text decides.
        var forwarded = Forward("""
            public sealed class Page(IStringLocale<Page> locale)
            {
                private string Text(string key)
                {
                    try
                    {
                        return int.Parse(key).ToString();
                    }
                    catch (FormatException)
                    {
                    }

                    return locale[key].Value;
                }

                public string Show() => Text("Today");
            }
            """);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(forwarded.ByProject),
            () => Assert.Single(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_check_a_calls_text_a_lookup_reads_inside_a_try()
    {
        // Act
        var forwarded = Forward("""
            public sealed class Page(IStringLocale<Page> locale)
            {
                private string Text(string key)
                {
                    try
                    {
                        return locale[key].Value;
                    }
                    catch (InvalidOperationException)
                    {
                        return key;
                    }
                }

                public string Show() => Text("Today");
            }
            """);

        // Assert
        Assert.Equal("Today", Assert.Single(Assert.Single(forwarded.ByProject).Value).Key.Text);
    }

    [Fact]
    public void Should_leave_unread_a_key_a_lambda_reads()
    {
        // Act — the lambda may never run, or run where the call's text is no longer what it looks up.
        var forwarded = Forward("""
            public sealed class Page(IStringLocale<Page> locale)
            {
                private Func<string> Text(string key) => () => locale[key].Value;

                public string Show() => Text("Today")();
            }
            """);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(forwarded.ByProject),
            () => Assert.Single(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_settle_a_Visual_Basic_condition_on_the_key_by_each_calls_text()
    {
        // Arrange — Visual Basic compares text with Nothing as it compares it with "", so neither call with them reads anything.
        var compilation = CompilationTestFactory.ReferencedVisualBasic("""
            Imports Forma.Framework.Localization

            Namespace Scratch
                Public NotInheritable Class Page
                    Private ReadOnly Property L As IStringLocale(Of Page)

                    Private Function Text(key As String) As String
                        If key = "" Then
                            Return key
                        End If

                        Return L(key).Value
                    End Function

                    Public Function Show() As String
                        Return Text(Nothing) & Text("") & Text("Today")
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var forwarded = LocaleFlowTestFactory.Forward(compilation);

        // Assert
        var site = Assert.Single(Assert.Single(forwarded.ByProject).Value);
        Assert.Multiple(
            () => Assert.Equal("Today", site.Key.Text),
            () => Assert.Empty(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_leave_out_a_call_whose_text_an_earlier_return_keeps_from_the_lookup()
    {
        // Act — the method returns before the lookup when its text is empty, so that call looks nothing up.
        var forwarded = Forward("""
            public sealed class Page(IStringLocale<Page> locale)
            {
                private string Text(string key)
                {
                    if (key.Length == 0)
                    {
                        return key;
                    }

                    return locale[key].Value;
                }

                public string Show() => Text("") + Text("Today");
            }
            """);

        // Assert
        var site = Assert.Single(Assert.Single(forwarded.ByProject).Value);
        Assert.Multiple(
            () => Assert.Equal("Today", site.Key.Text),
            () => Assert.Empty(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_leave_unforwarded_a_key_its_method_writes_before_the_lookup()
    {
        // Act — the lookup reads the trimmed text, not the text the call wrote.
        var forwarded = Forward("""
            public sealed class Page(IStringLocale<Page> locale)
            {
                private string Text(string key)
                {
                    key = key.Trim();

                    return locale[key].Value;
                }

                public string Show() => Text(" Today ");
            }
            """);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(forwarded.ByProject),
            () => Assert.Single(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_leave_out_a_call_whose_receiver_runs_an_override()
    {
        // Act — the override never reaches the lookup, so the key it is given is not looked up.
        var forwarded = Forward("""
            public class Base(IStringLocale<Base> locale)
            {
                public virtual string Label(string key) => locale[key].Value;
            }

            public sealed class Derived(IStringLocale<Base> locale) : Base(locale)
            {
                public override string Label(string key) => key;
            }

            public sealed class Page
            {
                public string Show()
                {
                    Base item = new Derived(default!);

                    return item.Label("Today");
                }
            }
            """);

        // Assert
        Assert.Empty(forwarded.ByProject);
    }

    [Fact]
    public void Should_check_a_call_to_a_compiled_method_a_hint_says_looks_its_key_up_for_the_type_its_type_argument_names()
    {
        // Arrange — the run holds the library's reference assembly alone, so only the hint tells what its method looks up.
        var compilation = ReferencingLibrary("""
            public sealed class Page
            {
                public LocalizableText Show() => Contoso.Widgets.Labels.Text<Page>("Today");
            }
            """);

        // Act
        var (lookups, _, diagnostics) = LocaleFlowTestFactory.Keyed([LocaleFlowTestFactory.KeyHint("Contoso.Widgets", "Contoso.Widgets.Labels", "Text", typeParameter: "TSource")], compilation);

        // Assert
        var site = Assert.Single(lookups);
        Assert.Multiple(
            () => Assert.Equal("Today", site.Key.Text),
            () => Assert.Equal("Page", Assert.Single(site.Origins).Type.Name),
            () => Assert.Equal(LocalizerArguments.Unknown, site.Arguments),
            () => Assert.Empty(diagnostics));
    }

    [Fact]
    public void Should_check_a_call_to_a_compiled_method_a_hint_says_looks_its_key_up_in_a_member_of_its_receiver()
    {
        // Arrange
        var compilation = ReferencingLibrary("""
            public sealed class Page(Contoso.Widgets.Toolbar toolbar)
            {
                public string Show() => toolbar.Label("Save");
            }
            """);

        // Act
        var (lookups, _, _) = LocaleFlowTestFactory.Keyed([LocaleFlowTestFactory.KeyHint("Contoso.Widgets", "Contoso.Widgets.Toolbar", "Label", locale: "L")], compilation);

        // Assert
        var site = Assert.Single(lookups);
        Assert.Multiple(
            () => Assert.Equal("Save", site.Key.Text),
            () => Assert.Equal("Toolbar", Assert.Single(site.Origins).Type.Name));
    }

    [Fact]
    public void Should_check_a_call_to_a_compiled_method_a_hint_says_looks_its_key_up_in_a_locale_it_is_passed()
    {
        // Arrange
        var compilation = ReferencingLibrary("""
            public sealed class Page(IStringLocale<Page> locale)
            {
                public string Show() => Contoso.Widgets.Labels.Translate(locale, "Close");
            }
            """);

        // Act
        var (lookups, _, _) = LocaleFlowTestFactory.Keyed([LocaleFlowTestFactory.KeyHint("Contoso.Widgets", "Contoso.Widgets.Labels", "Translate", locale: "locale")], compilation);

        // Assert
        var site = Assert.Single(lookups);
        Assert.Multiple(
            () => Assert.Equal("Close", site.Key.Text),
            () => Assert.Equal("Page", Assert.Single(site.Origins).Type.Name));
    }

    [Fact]
    public void Should_follow_a_key_a_method_passes_on_to_a_compiled_method_a_hint_names()
    {
        // Arrange
        var compilation = ReferencingLibrary("""
            public sealed class Page
            {
                private static LocalizableText Title(string key) => Contoso.Widgets.Labels.Text<Page>(key);

                public LocalizableText Show() => Title("Today");
            }
            """);

        // Act
        var (_, forwarded, _) = LocaleFlowTestFactory.Keyed([LocaleFlowTestFactory.KeyHint("Contoso.Widgets", "Contoso.Widgets.Labels", "Text", typeParameter: "TSource")], compilation);

        // Assert
        var site = Assert.Single(Assert.Single(forwarded.ByProject).Value);
        Assert.Multiple(
            () => Assert.Equal("Today", site.Key.Text),
            () => Assert.Equal("Page", Assert.Single(site.Origins).Type.Name),
            () => Assert.Empty(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_check_a_call_to_each_overload_of_a_compiled_method_a_hint_names()
    {
        // Arrange
        var compilation = ReferencingLibrary("""
            public sealed class Page
            {
                public string Show() => Contoso.Widgets.Captions.Caption<Page>("Today") + Contoso.Widgets.Captions.Caption<Page>("Stored {Count}", 3);
            }
            """);

        // Act
        var (lookups, _, diagnostics) = LocaleFlowTestFactory.Keyed([LocaleFlowTestFactory.KeyHint("Contoso.Widgets", "Contoso.Widgets.Captions", "Caption", typeParameter: "TSource")], compilation);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["Stored {Count}", "Today"], lookups.Select(site => site.Key.Text).Order(StringComparer.Ordinal)),
            () => Assert.All(lookups, site => Assert.Equal("Page", Assert.Single(site.Origins).Type.Name)),
            () => Assert.Empty(diagnostics));
    }

    [Fact]
    public void Should_leave_a_call_to_a_compiled_method_no_hint_names_unread()
    {
        // Arrange
        var compilation = ReferencingLibrary("""
            public sealed class Page
            {
                public LocalizableText Show() => Contoso.Widgets.Labels.Text<Page>("Today");
            }
            """);

        // Act
        var (lookups, _, _) = LocaleFlowTestFactory.Keyed([], compilation);

        // Assert
        Assert.Empty(lookups);
    }

    [Fact]
    public void Should_answer_for_a_librarys_consumers_where_a_hint_its_method_bears_out_says_the_method_looks_its_key_up()
    {
        // Act — the run's own call is checked, and the hint answers for the calls its consumers make, which their own runs read.
        var (_, forwarded, diagnostics) = LocaleFlowTestFactory.Keyed(
            [LocaleFlowTestFactory.KeyHint("Scratch", "Scratch.Labels", "Label", typeParameter: "TSource")],
            CompilationTestFactory.Referenced($$"""
                {{Usings}}namespace Scratch;

                public static class Labels
                {
                    public static LocalizableText Label<TSource>(string key) => LocalizableText.For<TSource>(key);
                }

                internal sealed class Page
                {
                    public LocalizableText Show() => Labels.Label<Page>("Today");
                }
                """));

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Today", Assert.Single(Assert.Single(forwarded.ByProject).Value).Key.Text),
            () => Assert.Empty(forwarded.Unforwarded),
            () => Assert.Empty(diagnostics));
    }

    [Fact]
    public void Should_bear_out_a_hint_for_a_method_passing_its_key_on_to_one_that_looks_it_up()
    {
        // Act — the public method passes the key and its type on to a private one, which looks it up.
        var (_, forwarded, diagnostics) = LocaleFlowTestFactory.Keyed(
            [LocaleFlowTestFactory.KeyHint("Scratch", "Scratch.Labels", "Label", typeParameter: "TSource")],
            CompilationTestFactory.Referenced($$"""
                {{Usings}}namespace Scratch;

                public static class Labels
                {
                    public static LocalizableText Label<TSource>(string key) => Text<TSource>(key);

                    private static LocalizableText Text<TPage>(string text) => LocalizableText.For<TPage>(text);
                }
                """));

        // Assert
        Assert.Multiple(
            () => Assert.Empty(diagnostics),
            () => Assert.Empty(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_report_a_hint_for_a_method_passing_its_key_on_with_another_type()
    {
        // Act
        var (_, _, diagnostics) = LocaleFlowTestFactory.Keyed(
            [LocaleFlowTestFactory.KeyHint("Scratch", "Scratch.Labels", "Label", typeParameter: "TSource")],
            CompilationTestFactory.Referenced($$"""
                {{Usings}}namespace Scratch;

                public static class Labels
                {
                    public static LocalizableText Label<TSource>(string key) => Text<Other>(key);

                    private static LocalizableText Text<TPage>(string text) => LocalizableText.For<TPage>(text);
                }

                public sealed class Other;
                """));

        // Assert
        Assert.Contains("looks 'key' up in another locale", Assert.Single(diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_report_a_hint_a_methods_body_contradicts_and_leave_its_key_unread()
    {
        // Act — the body looks the key up only when it is not raw text, which the text decides and the flow cannot tell.
        var (_, forwarded, diagnostics) = LocaleFlowTestFactory.Keyed(
            [LocaleFlowTestFactory.KeyHint("Scratch", "Scratch.Labels", "Label", locale: "locale")],
            CompilationTestFactory.Referenced($$"""
                {{Usings}}namespace Scratch;

                public static class Labels
                {
                    public static string Label(IStringLocale locale, string key) => key.StartsWith("raw:") ? key[4..] : locale[key].Value;
                }
                """));

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.ConflictingHint, diagnostic.Code),
            () => Assert.Contains("disagrees with its body", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Single(forwarded.Unforwarded));
    }

    [Fact]
    public void Should_report_a_hint_naming_a_method_whose_body_reads_its_locale_elsewhere()
    {
        // Act
        var (_, _, diagnostics) = LocaleFlowTestFactory.Keyed(
            [LocaleFlowTestFactory.KeyHint("Scratch", "Scratch.Labels", "Label", locale: "first")],
            CompilationTestFactory.Referenced($$"""
                {{Usings}}namespace Scratch;

                public static class Labels
                {
                    public static string Label(IStringLocale first, IStringLocale second, string key) => second[key].Value;
                }
                """));

        // Assert
        Assert.Contains("looks 'key' up in another locale", Assert.Single(diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_report_a_hint_naming_a_method_its_type_does_not_declare()
    {
        // Arrange
        var compilation = ReferencingLibrary("public sealed class Page;");

        // Act
        var (_, _, diagnostics) = LocaleFlowTestFactory.Keyed([LocaleFlowTestFactory.KeyHint("Contoso.Widgets", "Contoso.Widgets.Labels", "Missing", typeParameter: "TSource")], compilation);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.InvalidHint, diagnostic.Code),
            () => Assert.Equal("'Missing' is not a method of 'Contoso.Widgets.Labels' taking text as 'key', so the hint relates nothing.", diagnostic.Message));
    }

    [Fact]
    public void Should_report_a_hint_naming_a_method_a_call_may_run_an_override_of()
    {
        // Arrange
        var compilation = ReferencingLibrary("public sealed class Page;");

        // Act
        var (_, _, diagnostics) = LocaleFlowTestFactory.Keyed([LocaleFlowTestFactory.KeyHint("Contoso.Widgets", "Contoso.Widgets.Toolbar", "Hint", locale: "L")], compilation);

        // Assert
        Assert.Equal(LintingCodes.InvalidHint, Assert.Single(diagnostics).Code);
    }

    [Fact]
    public void Should_report_two_hints_relating_one_method_differently_and_apply_neither()
    {
        // Arrange
        var compilation = ReferencingLibrary("""
            public sealed class Page
            {
                public LocalizableText Show() => Contoso.Widgets.Labels.Pair<Page, Page>("Today");
            }
            """);

        // Act
        var (lookups, _, diagnostics) = LocaleFlowTestFactory.Keyed(
            [
                LocaleFlowTestFactory.KeyHint("Contoso.Widgets", "Contoso.Widgets.Labels", "Pair", typeParameter: "TSource", line: 1),
                LocaleFlowTestFactory.KeyHint("Contoso.Widgets", "Contoso.Widgets.Labels", "Pair", typeParameter: "TOther", line: 2),
            ],
            compilation);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(lookups),
            () => Assert.Equal(2, diagnostics.Count(diagnostic => diagnostic.Code == LintingCodes.ConflictingHint)));
    }

    private const string Usings = """
        using System;

        using Forma.Framework.Localization;

        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection.Extensions;

        """;

    // Forma's component model in small: a base reads its locale from a context its injected factory makes, for the type its
    // virtual member returns, through a lazy value and a delegate.
    private const string ComponentModel = """
        namespace Microsoft.AspNetCore.Components
        {
            [AttributeUsage(AttributeTargets.Property)]
            public sealed class InjectAttribute : Attribute;
        }

        namespace Scratch
        {
            public interface IContextFactory
            {
                Context Create(Type componentType, Func<Type> localeSourceType);
            }

            public sealed class ContextFactory(IServiceProvider services) : IContextFactory
            {
                public Context Create(Type componentType, Func<Type> localeSourceType)
                    => new(() => services.GetStringLocaleFactory().Create(localeSourceType()));
            }

            public sealed class Context(Func<IStringLocale> locale)
            {
                private readonly Lazy<IStringLocale> _locale = new(locale);

                public IStringLocale Locale => _locale.Value;
            }

            public static class Registration
            {
                public static void Register(IServiceCollection services) => services.TryAddScoped<IContextFactory, ContextFactory>();
            }

            public abstract class FormaLike
            {
                [Microsoft.AspNetCore.Components.Inject]
                private IContextFactory ContextFactory { get; set; } = default!;

                private Context? _context;

                protected virtual Type LocaleSourceType => GetType();

                protected Context Context => _context ??= ContextFactory.Create(GetType(), () => LocaleSourceType);

                protected IStringLocale L => Context.Locale;
            }
        """;

    private const string TestFactoryRegistration = """
                public static void Register(IServiceCollection services) => services.AddSingleton<IStringLocaleFactory>(new StubLocaleFactory());
        """;

    // A product whose static method reads a locale from the host's factory, its own lookup through it, and a method passing a key to
    // it; and a test project looking a key up through the same method, with the members given.
    private static (LoadedProject Product, LoadedProject Tests) TestedTexts(string testMembers)
    {
        var product = CompilationTestFactory.Referenced(Usings + """
            namespace Scratch;

            public sealed class Panel;

            public static class Services
            {
                public static IServiceProvider Provider { get; set; } = default!;
            }

            public static class Texts
            {
                public static IStringLocale Locale() => Services.Provider.GetStringLocaleFactory().Create(typeof(Panel));
            }

            public static class Labels
            {
                public static string Caption(string key) => Texts.Locale()[key].Value;
            }

            public sealed class Page
            {
                public string Show() => Texts.Locale()["Today"].Value;
            }
            """).WithAssemblyName("Contoso.Product");
        var tests = CompilationTestFactory.Referenced(Usings + $$"""
            namespace Scratch.Tests;

            public sealed class StubLocaleFactory : IStringLocaleFactory
            {
                public IStringLocale Create(Type resourceSource) => throw new NotSupportedException();

                public IStringLocale Create(string baseName, string location) => throw new NotSupportedException();
            }

            public sealed class TextsTests
            {
            {{testMembers}}

                public string Read() => Scratch.Texts.Locale()["Bold"].Value;
            }
            """).WithAssemblyName("Contoso.Product.Tests").AddReferences(product.ToMetadataReference());

        return (new LoadedProject("Product.csproj", product, false, false), new LoadedProject("Tests.csproj", tests, false, false) { IsTestProject = true });
    }

    // Forma's component model as a product registers it, and a test project's own component reading through it, with the test's
    // registration of the context factory given.
    private static (LoadedProject Product, LoadedProject Tests) TestedComponent(string registration)
    {
        var product = CompilationTestFactory.Referenced(Usings + ComponentModel + "}").WithAssemblyName("Contoso.Product");
        var tests = CompilationTestFactory.Referenced(Usings + $$"""
            namespace Scratch.Tests;

            public sealed class FakeContextFactory : Scratch.IContextFactory
            {
                public Scratch.Context Create(Type componentType, Func<Type> localeSourceType) => throw new NotSupportedException();
            }

            public sealed class TestedWidget : Scratch.FormaLike
            {
                public static void Register(IServiceCollection services) => {{registration}};

                public string Show() => L["Tested"].Value;
            }
            """).WithAssemblyName("Contoso.Product.Tests").AddReferences(product.ToMetadataReference());

        return (new LoadedProject("Product.csproj", product, false, false), new LoadedProject("Tests.csproj", tests, false, false) { IsTestProject = true });
    }

    private static IReadOnlyList<LocalizerLookup> Resolve(string declarations)
    {
        var namespaced = declarations.Contains("namespace Scratch\n{", StringComparison.Ordinal) || declarations.Contains("namespace Scratch\r\n{", StringComparison.Ordinal)
            ? $"{Usings}{declarations}\n}}"
            : $"{Usings}namespace Scratch;\n\n{declarations}";

        return LocaleFlowTestFactory.Resolve(CompilationTestFactory.Referenced(namespaced));
    }

    private static ForwardedLookups Forward(string declarations)
        => LocaleFlowTestFactory.Forward(CompilationTestFactory.Referenced($"{Usings}namespace Scratch;\n\n{declarations}"));

    private static LocalizerLookup Single(IReadOnlyList<LocalizerLookup> sites, string key)
        => Assert.Single(sites, site => site.Key.Text == key);

    private const string Component = """
        public abstract class Component
        {
            protected virtual Type LocalizationSourceType => GetType();

            protected IStringLocale L => default!;
        }
        """;

    // The component's locale is opaque to the flow, so the hint relating it to the type member is what names its type.
    private static readonly LocaleMemberHint ComponentHint = LocaleFlowTestFactory.Hint("Scratch", "Scratch.Component");

    private static readonly LocaleMemberHint LibraryHint = LocaleFlowTestFactory.Hint("Contoso.Components", "Contoso.Components.Component");

    private static Compilation ComponentCompilation(string members = "")
        => CompilationTestFactory.Referenced($$"""
            using System;

            using Forma.Framework.Localization;

            namespace Scratch;

            public abstract class Component
            {
                protected virtual Type LocalizationSourceType => GetType();

                protected IStringLocale L => default!;

                {{members}}
            }

            public sealed class Widget : Component
            {
                public void Show() => _ = L["Today"];
            }
            """);

    // A library whose methods look a key up, compiled to the reference assembly the build hands its consumers, holding no body.
    private static Compilation ReferencingLibrary(string declarations)
    {
        var library = CompilationTestFactory.Referenced("""
            using Forma.Framework.Localization;

            namespace Contoso.Widgets;

            public static class Labels
            {
                public static LocalizableText Text<TSource>(string key) => LocalizableText.For<TSource>(key);

                public static LocalizableText Pair<TSource, TOther>(string key) => LocalizableText.For<TOther>(key);

                public static string Translate(IStringLocale locale, string key) => locale[key].Value;
            }

            public static class Captions
            {
                public static string Caption<TSource>(string key) => LocalizableText.For<TSource>(key).ToString()!;

                public static string Caption<TSource>(string key, params object[] values) => LocalizableText.For<TSource>(key, values).ToString()!;
            }

            public class Toolbar(IStringLocale<Toolbar> locale)
            {
                public IStringLocale<Toolbar> L => locale;

                public string Label(string key) => L[key].Value;

                public virtual string Hint(string key) => L[key].Value;
            }
            """).WithAssemblyName("Contoso.Widgets");

        return CompilationTestFactory.Referenced($"{Usings}namespace Scratch;\n\n{declarations}").AddReferences(CompilationTestFactory.MetadataOf(library));
    }

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

        return LocaleFlowTestFactory.Resolve([ComponentHint], compilation);
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
                    Protected ReadOnly Property L As IStringLocale
                        Get
                            Return Nothing
                        End Get
                    End Property
                End Class

                {declarations}
            End Namespace
            """);

        return LocaleFlowTestFactory.Resolve([ComponentHint], compilation);
    }
}
