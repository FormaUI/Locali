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
        var site = Assert.Single(LocaleFlowTestFactory.Resolve(compilation));

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
        var site = Assert.Single(LocaleFlowTestFactory.Resolve(compilation, components));

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

    private static IReadOnlyList<LocalizerLookup> Resolve(string declarations)
    {
        var namespaced = declarations.Contains("namespace Scratch\n{", StringComparison.Ordinal) || declarations.Contains("namespace Scratch\r\n{", StringComparison.Ordinal)
            ? $"{Usings}{declarations}\n}}"
            : $"{Usings}namespace Scratch;\n\n{declarations}";

        return LocaleFlowTestFactory.Resolve(CompilationTestFactory.Referenced(namespaced));
    }

    private static LocalizerLookup Single(IReadOnlyList<LocalizerLookup> sites, string key)
        => Assert.Single(sites, site => site.Key.Text == key);

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

        return LocaleFlowTestFactory.Resolve(compilation);
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

        return LocaleFlowTestFactory.Resolve(compilation);
    }
}
