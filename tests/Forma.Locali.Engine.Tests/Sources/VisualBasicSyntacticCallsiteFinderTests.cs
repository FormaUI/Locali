namespace Forma.Locali.Tests.Sources;

public sealed class VisualBasicSyntacticCallsiteFinderTests
{
    [Fact]
    public void Should_read_a_key_from_a_default_property_on_a_declared_localizer()
    {
        // Arrange
        var compilation = CompilationTestFactory.UnreferencedVisualBasic("""
            Namespace Scratch
                Public NotInheritable Class Widget
                    Private keys As IStringLocale(Of Widget)

                    Public Function Read() As Object
                        Return keys("Today")
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var sites = new VisualBasicSyntacticCallsiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        var site = Assert.Single(sites);

        Assert.Multiple(
            () => Assert.Equal("Today", site.Key.Text),
            () => Assert.Equal(1, site.Key.TextOffset),
            () => Assert.Equal(LocalizerArguments.None, site.Arguments));
    }

    [Fact]
    public void Should_read_a_key_from_a_resolve_call_on_a_declared_localizer()
    {
        // Arrange
        var compilation = CompilationTestFactory.UnreferencedVisualBasic("""
            Namespace Scratch
                Public NotInheritable Class Widget
                    Private keys As IStringLocale(Of Widget)

                    Public Function Read() As Object
                        Return keys.Resolve("Today")
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var sites = new VisualBasicSyntacticCallsiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.Equal(["Today"], sites.Select(site => site.Key.Text));
    }

    [Fact]
    public void Should_read_a_receiver_however_it_is_cased()
    {
        // Arrange — Visual Basic reads Keys and keys as one name.
        var compilation = CompilationTestFactory.UnreferencedVisualBasic("""
            Namespace Scratch
                Public NotInheritable Class Widget
                    Private keys As istringlocale(Of Widget)

                    Public Function Read() As Object
                        Return Keys("Today")
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var sites = new VisualBasicSyntacticCallsiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.Equal(["Today"], sites.Select(site => site.Key.Text));
    }

    [Fact]
    public void Should_ignore_a_default_property_on_something_that_is_not_a_localizer()
    {
        // Arrange
        var compilation = CompilationTestFactory.UnreferencedVisualBasic("""
            Namespace Scratch
                Public NotInheritable Class Widget
                    Private keys As Dictionary(Of String, String)

                    Public Function Read() As Object
                        Return keys("Today")
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var sites = new VisualBasicSyntacticCallsiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.Empty(sites);
    }

    [Fact]
    public void Should_ignore_a_receiver_that_is_declared_nowhere()
    {
        // Arrange
        var compilation = CompilationTestFactory.UnreferencedVisualBasic("""
            Namespace Scratch
                Public NotInheritable Class Widget
                    Public Function Read() As Object
                        Return whatever("Today")
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var sites = new VisualBasicSyntacticCallsiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.Empty(sites);
    }

    [Fact]
    public void Should_read_a_localizer_declared_in_another_partial()
    {
        // Arrange
        var compilation = CompilationTestFactory.UnreferencedVisualBasic(
            """
            Namespace Scratch
                Partial Public Class Widget
                    Private keys As IStringLocale(Of Widget)
                End Class
            End Namespace
            """,
            """
            Namespace Scratch
                Partial Public Class Widget
                    Public Function Read() As Object
                        Return keys("Today")
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var sites = new VisualBasicSyntacticCallsiteFinder().Find(compilation, compilation.SyntaxTrees.Last()).Lookups;

        // Assert
        Assert.Equal(["Today"], sites.Select(site => site.Key.Text));
    }

    [Fact]
    public void Should_not_read_a_localizer_declared_on_a_different_type()
    {
        // Arrange
        var compilation = CompilationTestFactory.UnreferencedVisualBasic(
            """
            Namespace Scratch
                Public NotInheritable Class Other
                    Private keys As IStringLocale(Of Other)
                End Class
            End Namespace
            """,
            """
            Namespace Scratch
                Public NotInheritable Class Widget
                    Public Function Read() As Object
                        Return keys("Today")
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var sites = new VisualBasicSyntacticCallsiteFinder().Find(compilation, compilation.SyntaxTrees.Last()).Lookups;

        // Assert
        Assert.Empty(sites);
    }

    [Fact]
    public void Should_read_a_localizer_declared_as_a_property()
    {
        // Arrange
        var compilation = CompilationTestFactory.UnreferencedVisualBasic("""
            Namespace Scratch
                Public NotInheritable Class Widget
                    Private ReadOnly Property Keys As IStringLocale(Of Widget)

                    Public Function Read() As Object
                        Return Keys("Today")
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var sites = new VisualBasicSyntacticCallsiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.Equal(["Today"], sites.Select(site => site.Key.Text));
    }

    [Fact]
    public void Should_read_a_localizer_declared_as_a_parameter()
    {
        // Arrange
        var compilation = CompilationTestFactory.UnreferencedVisualBasic("""
            Namespace Scratch
                Public NotInheritable Class Widget
                    Public Shared Function Read(keys As IStringLocale(Of Widget)) As Object
                        Return keys("Today")
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var sites = new VisualBasicSyntacticCallsiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.Equal(["Today"], sites.Select(site => site.Key.Text));
    }

    [Fact]
    public void Should_not_let_a_local_in_one_member_reach_another()
    {
        // Arrange
        var compilation = CompilationTestFactory.UnreferencedVisualBasic("""
            Namespace Scratch
                Public NotInheritable Class Widget
                    Public Function Declaring() As Object
                        Dim keys As IStringLocale(Of Widget) = Nothing

                        Return keys("Today")
                    End Function

                    Public Function Borrowing() As Object
                        Return keys("Tomorrow")
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var sites = new VisualBasicSyntacticCallsiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.Equal(["Today"], sites.Select(site => site.Key.Text));
    }

    [Fact]
    public void Should_read_a_localizer_named_by_its_namespace()
    {
        // Arrange
        var compilation = CompilationTestFactory.UnreferencedVisualBasic("""
            Namespace Scratch
                Public NotInheritable Class Widget
                    Private keys As Forma.Framework.Localization.IStringLocale(Of Widget)

                    Public Function Read() As Object
                        Return keys("Today")
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var sites = new VisualBasicSyntacticCallsiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.Equal(["Today"], sites.Select(site => site.Key.Text));
    }

    [Fact]
    public void Should_leave_a_lookup_a_localizer_forwards_alone()
    {
        // Arrange — a type implementing the contract passes a key on rather than writing one.
        var compilation = CompilationTestFactory.UnreferencedVisualBasic("""
            Namespace Scratch
                Public NotInheritable Class Forwarding
                    Implements IStringLocale

                    Private inner As IStringLocale

                    Public Function Read(name As String) As Object
                        Return inner(name)
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var found = new VisualBasicSyntacticCallsiteFinder().Find(compilation, compilation.SyntaxTrees.Single());

        // Assert
        Assert.Multiple(
            () => Assert.Empty(found.Lookups),
            () => Assert.Empty(found.UnreadableKeys));
    }

    [Fact]
    public void Should_report_a_key_written_as_an_expression()
    {
        // Arrange
        var compilation = CompilationTestFactory.UnreferencedVisualBasic("""
            Namespace Scratch
                Public NotInheritable Class Widget
                    Private keys As IStringLocale(Of Widget)

                    Public Function Read(column As String) As Object
                        Return keys(column)
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var found = new VisualBasicSyntacticCallsiteFinder().Find(compilation, compilation.SyntaxTrees.Single());

        // Assert
        Assert.Single(found.UnreadableKeys);
    }

    [Fact]
    public void Should_report_an_interpolated_key()
    {
        // Arrange
        var compilation = CompilationTestFactory.UnreferencedVisualBasic("""
            Namespace Scratch
                Public NotInheritable Class Widget
                    Private keys As IStringLocale(Of Widget)

                    Public Function Read(count As Integer) As Object
                        Return keys($"Stored {count}.")
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var found = new VisualBasicSyntacticCallsiteFinder().Find(compilation, compilation.SyntaxTrees.Single());

        // Assert
        Assert.Multiple(
            () => Assert.Single(found.InterpolatedKeys),
            () => Assert.Empty(found.UnreadableKeys));
    }

    [Fact]
    public void Should_tell_a_lookup_passed_to_string_format_from_one_that_is_not()
    {
        // Arrange
        var compilation = CompilationTestFactory.UnreferencedVisualBasic("""
            Namespace Scratch
                Public NotInheritable Class Widget
                    Private keys As IStringLocale(Of Widget)

                    Public Function Show(count As Integer) As Object
                        Return String.Format(keys("Stored {Count}."), count)
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var sites = new VisualBasicSyntacticCallsiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.True(Assert.Single(sites).IsFormatString);
    }

    [Fact]
    public void Should_leave_a_lookup_at_a_later_position_alone_without_a_model()
    {
        // Arrange — no bound overload exists to confirm the lookup answers the format parameter rather than a value.
        var compilation = CompilationTestFactory.UnreferencedVisualBasic("""
            Namespace Scratch
                Public NotInheritable Class Widget
                    Private keys As IStringLocale(Of Widget)

                    Public Function Show(count As Integer) As Object
                        Return String.Format("Searched for {0}.", keys("Query"))
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var sites = new VisualBasicSyntacticCallsiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.False(Assert.Single(sites).IsFormatString);
    }

    [Fact]
    public void Should_leave_a_lookup_named_for_a_different_parameter_alone_without_a_model()
    {
        // Arrange — the lookup sits first in source but explicitly claims a value parameter's name.
        var compilation = CompilationTestFactory.UnreferencedVisualBasic("""
            Namespace Scratch
                Public NotInheritable Class Widget
                    Private keys As IStringLocale(Of Widget)

                    Public Function Show() As Object
                        Return String.Format(arg0:=keys("Query"), format:="Searched for {0}.")
                    End Function
                End Class
            End Namespace
            """);

        // Act
        var sites = new VisualBasicSyntacticCallsiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.False(Assert.Single(sites).IsFormatString);
    }

    [Fact]
    public void Should_read_a_localizer_a_lambda_in_a_field_takes()
    {
        // Arrange — the lambda's typed parameter is scoped to the field that writes it, which no method block holds.
        var compilation = CompilationTestFactory.UnreferencedVisualBasic("""
            Namespace Scratch
                Public NotInheritable Class Widget
                    Private ReadOnly reader As Func(Of IStringLocale(Of Widget), Object) = Function(keys As IStringLocale(Of Widget)) keys("Today")
                End Class
            End Namespace
            """);

        // Act
        var sites = new VisualBasicSyntacticCallsiteFinder().Find(compilation, compilation.SyntaxTrees.Single()).Lookups;

        // Assert
        Assert.Equal(["Today"], sites.Select(site => site.Key.Text));
    }

    [Fact]
    public void Should_answer_for_Visual_Basic_alone()
    {
        // Act & Assert — the C# twin reads the rest.
        Assert.False(new VisualBasicSyntacticCallsiteFinder().CanRead(CompilationTestFactory.Unreferenced("class Widget;")));
    }
}
