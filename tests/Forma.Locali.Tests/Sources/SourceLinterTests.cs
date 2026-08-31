namespace Forma.Locali.Tests.Sources;

using Forma.Locali.Projects;

using Microsoft.CodeAnalysis;

public sealed class SourceLinterTests
{
    [Fact]
    public void Should_report_a_key_written_as_an_expression()
    {
        // Act
        var diagnostics = Lint("""public void Show(string column) => _ = L[column];""");

        // Assert
        var diagnostic = Assert.Single(diagnostics);

        Assert.Multiple(
            () => Assert.Equal(LintCodes.KeyNotAnalysed, diagnostic.Code),
            () => Assert.Equal(LintSeverity.Warning, diagnostic.Severity));
    }

    [Fact]
    public void Should_report_a_key_written_as_a_call()
    {
        // Act
        var diagnostics = Lint("""public void Show(object column) => _ = L[column.ToString()];""");

        // Assert
        Assert.Equal(LintCodes.KeyNotAnalysed, Assert.Single(diagnostics).Code);
    }

    [Fact]
    public void Should_leave_a_key_written_as_a_literal_alone()
    {
        // Act
        var diagnostics = Lint("""public void Show() => _ = L["Today"];""");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_key_written_as_a_constant_alone()
    {
        // Arrange — the compiler folds it, so the key is as readable as one spelled out.
        const string Body = """
            private const string Key = "Today";
            public void Show() => _ = L[Key];
            """;

        // Act
        var diagnostics = Lint(Body);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_read_the_key_a_constant_holds()
    {
        // Arrange
        const string Body = """
            private const string Key = "Stored {stored}.";
            public void Show() => _ = L[Key];
            """;

        // Act
        var site = Assert.Single(Sites(Body));

        // Assert
        Assert.Equal("Stored {stored}.", site.Key);
    }

    [Fact]
    public void Should_read_the_key_a_folded_concatenation_holds()
    {
        // Act
        var site = Assert.Single(Sites("""public void Show() => _ = L["Stored " + "here."];"""));

        // Assert
        Assert.Equal("Stored here.", site.Key);
    }

    [Fact]
    public void Should_leave_a_lookup_a_localizer_forwards_alone()
    {
        // Arrange — a type implementing the contract passes a key on rather than writing one.
        var compilation = CompilationTestFactory.Referenced("""
            using Forma.Framework.Localization;

            namespace Scratch;

            public sealed class Forwarding(IStringLocale inner) : IStringLocale
            {
                public LocalizedText this[string name] => inner[name];
                public LocalizedText this[string name, params ReadOnlySpan<LocalizedArgument> arguments] => inner[name, arguments];
                public LocalizedText this[string name, params ReadOnlySpan<object?> values] => inner[name, values];
                public LocalizedText this[LocalizedFormatHandler handler] => inner[handler];
                public LocalizedPattern Resolve(string name) => inner.Resolve(name);
                public LocalizedPattern Resolve(string name, params LocalizedArgument[] arguments) => inner.Resolve(name, arguments);
                public LocalizedPattern Resolve(string name, params object?[] values) => inner.Resolve(name, values);
                public LocalizedPattern Resolve(LocalizedFormatHandler handler) => inner.Resolve(handler);
                public IEnumerable<LocalizedText> GetAllStrings(bool includeParentCultures) => inner.GetAllStrings(includeParentCultures);
            }
            """);

        // Act
        var diagnostics = Reported(compilation);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_forwarding_localizer_alone_without_a_model()
    {
        // Arrange — nothing binds here, so the syntactic path reads the base list instead of a symbol.
        var compilation = CompilationTestFactory.Unreferenced("""
            namespace Scratch;

            public sealed class Forwarding : IStringLocale
            {
                private IStringLocale inner;

                public object Read(string name) => inner[name];
            }
            """);

        // Act
        var diagnostics = Reported(compilation);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_forwarded_key_from_a_type_that_is_no_localizer()
    {
        // Arrange — the same lookup, on a type that only holds a localizer, is a genuine blind spot.
        var compilation = CompilationTestFactory.Unreferenced("""
            namespace Scratch;

            public sealed class Wrapper
            {
                private IStringLocale inner;

                public object Read(string name) => inner[name];
            }
            """);

        // Act
        var diagnostics = Reported(compilation);

        // Assert
        Assert.Equal(LintCodes.KeyNotAnalysed, Assert.Single(diagnostics).Code);
    }

    private static IReadOnlyList<LintDiagnostic> Lint(string body)
        => Reported(Compile(body));

    private static IReadOnlyList<LocalizerCallSite> Sites(string body)
        => Result(Compile(body)).CallSites;

    private static IReadOnlyList<LintDiagnostic> Reported(Compilation compilation)
        => [.. Result(compilation).Diagnostics.Where(diagnostic => diagnostic.Code == LintCodes.KeyNotAnalysed)];

    private static SourceLintResult Result(Compilation compilation)
        => new SourceLinter(
                new SemanticCallSiteFinder(),
                new SyntacticCallSiteFinder(),
                SourceLinterTestFactory.Checkers())
            .Lint(
                new LoadedProject("Scratch.csproj", compilation, HasGeneratedSources: false, HasRazorMarkup: false),
                [.. compilation.SyntaxTrees.Select(tree => tree.FilePath)]);

    private static Compilation Compile(string body)
        => CompilationTestFactory.Referenced($$"""
            using Forma.Framework.Localization;

            namespace Scratch;

            public sealed class Widget
            {
                private IStringLocale<Widget> L { get; } = default!;

                {{body}}
            }
            """);
}
