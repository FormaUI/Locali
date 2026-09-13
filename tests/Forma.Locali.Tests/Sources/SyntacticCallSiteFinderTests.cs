namespace Forma.Locali.Tests.Sources;

public sealed class SyntacticCallSiteFinderTests
{
    [Fact]
    public void Should_read_a_key_from_an_indexer_on_a_declared_localizer()
    {
        // Arrange
        var compilation = CompilationTestFactory.Unreferenced("""
            namespace Scratch;

            public sealed class Widget
            {
                private IStringLocale<Widget> keys;

                public object Read() => keys["Today"];
            }
            """);

        // Act
        var sites = new SyntacticCallSiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.Equal(["Today"], sites.Select(site => site.Key.Text));
    }

    [Fact]
    public void Should_read_a_key_from_a_resolve_call_on_a_declared_localizer()
    {
        // Arrange
        var compilation = CompilationTestFactory.Unreferenced("""
            namespace Scratch;

            public sealed class Widget
            {
                private IStringLocale<Widget> keys;

                public object Read() => keys.Resolve("Today");
            }
            """);

        // Act
        var sites = new SyntacticCallSiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.Equal(["Today"], sites.Select(site => site.Key.Text));
    }

    [Fact]
    public void Should_ignore_an_indexer_on_something_that_is_not_a_localizer()
    {
        // Arrange
        var compilation = CompilationTestFactory.Unreferenced("""
            namespace Scratch;

            public sealed class Widget
            {
                private Dictionary<string, string> keys;

                public object Read() => keys["Today"];
            }
            """);

        // Act
        var sites = new SyntacticCallSiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.Empty(sites);
    }

    [Fact]
    public void Should_ignore_a_receiver_that_is_declared_nowhere()
    {
        // Arrange
        var compilation = CompilationTestFactory.Unreferenced("""
            namespace Scratch;

            public sealed class Widget
            {
                public object Read() => whatever["Today"];
            }
            """);

        // Act
        var sites = new SyntacticCallSiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.Empty(sites);
    }

    [Fact]
    public void Should_read_a_localizer_declared_in_another_partial()
    {
        // Arrange
        var compilation = CompilationTestFactory.Unreferenced(
            """
            namespace Scratch;

            public partial class Widget
            {
                private IStringLocale<Widget> keys;
            }
            """,
            """
            namespace Scratch;

            public partial class Widget
            {
                public object Read() => keys["Today"];
            }
            """);

        // Act
        var sites = new SyntacticCallSiteFinder().Find(compilation, compilation.SyntaxTrees.Last()).Lookups;

        // Assert
        Assert.Equal(["Today"], sites.Select(site => site.Key.Text));
    }

    [Fact]
    public void Should_not_read_a_localizer_declared_on_a_different_type()
    {
        // Arrange
        var compilation = CompilationTestFactory.Unreferenced(
            """
            namespace Scratch;

            public sealed class Other
            {
                private IStringLocale<Other> keys;
            }
            """,
            """
            namespace Scratch;

            public sealed class Widget
            {
                public object Read() => keys["Today"];
            }
            """);

        // Act
        var sites = new SyntacticCallSiteFinder().Find(compilation, compilation.SyntaxTrees.Last()).Lookups;

        // Assert
        Assert.Empty(sites);
    }

    [Fact]
    public void Should_read_a_localizer_declared_as_a_parameter()
    {
        // Arrange
        var compilation = CompilationTestFactory.Unreferenced("""
            namespace Scratch;

            public sealed class Widget
            {
                public static object Read(IStringLocale<Widget> keys) => keys["Today"];
            }
            """);

        // Act
        var sites = new SyntacticCallSiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.Equal(["Today"], sites.Select(site => site.Key.Text));
    }

    [Fact]
    public void Should_not_let_a_local_in_one_member_reach_another()
    {
        // Arrange
        var compilation = CompilationTestFactory.Unreferenced("""
            namespace Scratch;

            public sealed class Widget
            {
                public object Declaring()
                {
                    IStringLocale<Widget> keys = null;

                    return keys["Today"];
                }

                public object Borrowing() => keys["Tomorrow"];
            }
            """);

        // Act
        var sites = new SyntacticCallSiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.Equal(["Today"], sites.Select(site => site.Key.Text));
    }

    [Fact]
    public void Should_read_a_localizer_named_by_its_namespace()
    {
        // Arrange
        var compilation = CompilationTestFactory.Unreferenced("""
            namespace Scratch;

            public sealed class Widget
            {
                private Forma.Framework.Localization.IStringLocale<Widget> keys;

                public object Read() => keys["Today"];
            }
            """);

        // Act
        var sites = new SyntacticCallSiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.Equal(["Today"], sites.Select(site => site.Key.Text));
    }

    [Fact]
    public void Should_tell_a_lookup_passed_to_string_format_from_one_that_is_not()
    {
        // Arrange
        var compilation = CompilationTestFactory.Unreferenced("""
            namespace Scratch;

            public sealed class Widget
            {
                private IStringLocale<Widget> keys;

                public object Show(int count) => string.Format(keys["Stored {Count}."], count);
            }
            """);

        // Act
        var sites = new SyntacticCallSiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.True(Assert.Single(sites).IsFormatString);
    }

    [Fact]
    public void Should_leave_a_lookup_at_a_later_position_alone_without_a_model()
    {
        // Arrange — no bound overload exists to confirm the lookup answers the format parameter rather than a value.
        var compilation = CompilationTestFactory.Unreferenced("""
            namespace Scratch;

            public sealed class Widget
            {
                private IStringLocale<Widget> keys;

                public object Show(int count) => string.Format("Searched for {0}.", keys["Query"]);
            }
            """);

        // Act
        var sites = new SyntacticCallSiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.False(Assert.Single(sites).IsFormatString);
    }

    [Fact]
    public void Should_leave_a_lookup_named_for_a_different_parameter_alone_without_a_model()
    {
        // Arrange — the lookup sits first in source but explicitly claims a value parameter's name.
        var compilation = CompilationTestFactory.Unreferenced("""
            namespace Scratch;

            public sealed class Widget
            {
                private IStringLocale<Widget> keys;

                public object Show() => string.Format(arg0: keys["Query"], format: "Searched for {0}.");
            }
            """);

        // Act
        var sites = new SyntacticCallSiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.False(Assert.Single(sites).IsFormatString);
    }
}
