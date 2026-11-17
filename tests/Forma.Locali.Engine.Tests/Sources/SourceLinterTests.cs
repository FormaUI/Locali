namespace Forma.Locali.Tests.Sources;

using Forma.Locali.Flow;
using Forma.Locali.Projects;
using Forma.Locali.Tests.Support.Flow;

using Microsoft.CodeAnalysis;

public sealed class SourceLinterTests
{
    [Fact]
    public void Should_count_a_key_written_as_an_expression_as_unread_without_reporting_it()
    {
        // Act
        var result = Result(Compile("""
            private string Column { get; } = "Name";
            public void Show() => _ = L[Column];
            """));

        // Assert
        Assert.Multiple(
            () => Assert.Single(result.UnreadKeys),
            () => Assert.Empty(result.Diagnostics));
    }

    [Fact]
    public void Should_count_a_key_written_as_a_call_as_unread()
    {
        // Act
        var unread = UnreadKeys("""public void Show(object column) => _ = L[column.ToString()];""");

        // Assert
        Assert.Single(unread);
    }

    [Fact]
    public void Should_report_an_interpolated_key()
    {
        // Act
        var diagnostics = Lint("""public void Show(int count) => _ = L[$"Stored {count}."];""", LintingCodes.InterpolatedKey);

        // Assert
        var diagnostic = Assert.Single(diagnostics);

        Assert.Multiple(
            () => Assert.Equal(LintingCodes.InterpolatedKey, diagnostic.Code),
            () => Assert.Equal(LintingSeverity.Error, diagnostic.Severity));
    }

    [Fact]
    public void Should_report_an_interpolated_key_through_resolve()
    {
        // Act
        var diagnostics = Lint("""public void Show(int count) => _ = L.Resolve($"Stored {count}.");""", LintingCodes.InterpolatedKey);

        // Assert
        Assert.Equal(LintingCodes.InterpolatedKey, Assert.Single(diagnostics).Code);
    }

    [Fact]
    public void Should_report_an_interpolated_key_without_a_model()
    {
        // Arrange — nothing binds here, so the syntactic path reads the key's own syntax.
        var compilation = CompilationTestFactory.Unreferenced("""
            namespace Scratch;

            public sealed class Widget
            {
                private IStringLocale<Widget> L;

                public object Show(int count) => L[$"Stored {count}."];
            }
            """);

        // Act
        var diagnostics = Reported(compilation, LintingCodes.InterpolatedKey);

        // Assert
        Assert.Equal(LintingCodes.InterpolatedKey, Assert.Single(diagnostics).Code);
    }

    [Fact]
    public void Should_leave_an_interpolated_key_a_localizer_writes_alone()
    {
        // Arrange — a key composed inside an implementation belongs to the implementation, not to an entry.
        var compilation = CompilationTestFactory.Unreferenced("""
            namespace Scratch;

            public sealed class Prefixing : IStringLocale
            {
                private IStringLocale inner;

                public object Read(string name) => inner[$"Widget: {name}"];
            }
            """);

        // Act
        var diagnostics = Reported(compilation, LintingCodes.InterpolatedKey);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_lookup_through_Microsofts_localizer_to_the_entry_check_alone()
    {
        // Arrange — the localizer fills a composite key with string.Format, which no rule of Forma's reads, and a key it writes as an
        // expression is its own contract's.
        var compilation = CompilationTestFactory.Referenced(StringLocalizerFakeData.Declarations + """
            namespace Scratch
            {
                public sealed class Checkout(Microsoft.Extensions.Localization.IStringLocalizer<Checkout> localizer)
                {
                    public string Total(decimal amount) => localizer["Total: {0:C}", amount].Value;

                    public string Stored(int count) => localizer[$"Stored {count}."].Value;

                    public string Column(string column) => localizer[column].Value;
                }
            }
            """);

        // Act
        var result = Result(compilation);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(result.Diagnostics),
            () => Assert.True(Assert.Single(result.Callsites).UsesStringLocalizer));
    }

    [Fact]
    public void Should_leave_a_key_written_as_a_literal_alone()
    {
        // Act
        var unread = UnreadKeys("""public void Show() => _ = L["Today"];""");

        // Assert
        Assert.Empty(unread);
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
        var unread = UnreadKeys(Body);

        // Assert
        Assert.Empty(unread);
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
        var site = Assert.Single(Lookups(Body));

        // Assert
        Assert.Equal("Stored {stored}.", site.Key.Text);
    }

    [Fact]
    public void Should_read_the_key_a_folded_concatenation_holds()
    {
        // Act
        var site = Assert.Single(Lookups("""public void Show() => _ = L["Stored " + "here."];"""));

        // Assert
        Assert.Equal("Stored here.", site.Key.Text);
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
                public LocalizedText this[string name, params ReadOnlySpan<object?> values] => inner[name, values];
                public LocalizedPattern Resolve(string name) => inner.Resolve(name);
                public LocalizedPattern Resolve(string name, params object?[] values) => inner.Resolve(name, values);
                public IEnumerable<LocalizedText> GetAllStrings(bool includeParentCultures) => inner.GetAllStrings(includeParentCultures);
            }
            """);

        // Act
        var unread = UnreadKeys(compilation);

        // Assert
        Assert.Empty(unread);
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
        var unread = UnreadKeys(compilation);

        // Assert
        Assert.Empty(unread);
    }

    [Fact]
    public void Should_count_a_forwarded_key_from_a_type_that_is_no_localizer_as_unread()
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
        var unread = UnreadKeys(compilation);

        // Assert
        Assert.Single(unread);
    }

    [Fact]
    public void Should_count_a_key_a_call_writes_as_an_expression_as_unread()
    {
        // Act
        var unread = UnreadKeys("""
            public void Show(object column) => _ = LocalizableText.For<Widget>(column.ToString()!);
            """);

        // Assert
        Assert.Single(unread);
    }

    [Fact]
    public void Should_report_an_interpolated_key_a_call_writes()
    {
        // Act
        var diagnostics = Lint("""public void Show(int count) => _ = LocalizableText.For<Widget>($"Stored {count}.");""", LintingCodes.InterpolatedKey);

        // Assert
        Assert.Equal(LintingCodes.InterpolatedKey, Assert.Single(diagnostics).Code);
    }

    [Fact]
    public void Should_leave_a_key_a_parameter_passes_to_the_calls_of_its_method()
    {
        // Act — the calls write the key, and are read once every project of the run is loaded.
        var result = Result(Compile("""public LocalizableText Carry(string key) => LocalizableText.For<Widget>(key);"""));

        // Assert
        Assert.Multiple(
            () => Assert.Empty(result.Diagnostics),
            () => Assert.Equal("key", Assert.Single(result.ForwardedKeys).Parameter));
    }

    [Fact]
    public void Should_leave_a_key_a_lookup_takes_from_a_parameter_under_a_condition_to_the_calls_of_its_method()
    {
        // Act — whether the lookup reads a call's text is settled call by call, once every project of the run is loaded.
        var result = Result(Compile("""public string Text(string key) => key.Length == 0 ? string.Empty : L[key].Value;"""));

        // Assert
        Assert.Multiple(
            () => Assert.Empty(result.Diagnostics),
            () => Assert.Equal("key", Assert.Single(result.ForwardedKeys).Parameter));
    }

    [Fact]
    public void Should_report_a_project_loaded_without_its_restore_output()
    {
        // Arrange
        var compilation = Compile("""public void Show() => _ = L["Today"];""");
        var project = new LoadedProject("Scratch.csproj", compilation, HasGeneratedSources: false, HasRazorMarkup: false) { IsRestored = false };

        // Act
        var diagnostics = new SourceLinter(SourceLinterTestFactory.Finders(), SourceLinterTestFactory.Checkers())
            .Lint(project, [.. compilation.SyntaxTrees.Select(tree => tree.FilePath)], TestContext.Current.CancellationToken)
            .Diagnostics;

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Multiple(
            () => Assert.Equal(LintingCodes.NotRestored, diagnostic.Code),
            () => Assert.Equal(LintingSeverity.Error, diagnostic.Severity),
            () => Assert.Equal("Scratch.csproj", diagnostic.Path));
    }

    [Fact]
    public void Should_count_a_call_to_a_hinted_method_writing_its_key_as_an_expression_as_unread()
    {
        // Arrange — the library's method looks its key up, which only the hint tells, and the call writes the key as an expression.
        var library = CompilationTestFactory.Referenced("""
            using Forma.Framework.Localization;

            namespace Contoso.Widgets;

            public static class Labels
            {
                public static LocalizableText Text<TSource>(string key) => LocalizableText.For<TSource>(key);
            }
            """).WithAssemblyName("Contoso.Widgets");
        var compilation = CompilationTestFactory.Referenced("""
            namespace Scratch;

            public sealed class Page
            {
                public object Show(object column) => Contoso.Widgets.Labels.Text<Page>(column.ToString()!);
            }
            """).AddReferences(CompilationTestFactory.MetadataOf(library));
        var project = new LoadedProject("Scratch.csproj", compilation, HasGeneratedSources: false, HasRazorMarkup: false);
        var keyed = new LocaleFlow([project], [], [LocaleFlowTestFactory.KeyHint("Contoso.Widgets", "Contoso.Widgets.Labels", "Text", typeParameter: "TSource")]).KeyedMethods;

        // Act
        var result = new SourceLinter(SourceLinterTestFactory.Finders(), SourceLinterTestFactory.Checkers())
            .LintKeyed(project, [.. compilation.SyntaxTrees.Select(tree => tree.FilePath)], keyed, TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Single(result.UnreadKeys),
            () => Assert.Empty(result.Diagnostics));
    }

    private static IReadOnlyList<FileLinePositionSpan> UnreadKeys(string body)
        => UnreadKeys(Compile(body));

    private static IReadOnlyList<LintingDiagnostic> Lint(string body, LintingCode code)
        => Reported(Compile(body), code);

    private static IReadOnlyList<LocalizerLookup> Lookups(string body)
        => Result(Compile(body)).Callsites;

    private static IReadOnlyList<FileLinePositionSpan> UnreadKeys(Compilation compilation)
        => Result(compilation).UnreadKeys;

    private static IReadOnlyList<LintingDiagnostic> Reported(Compilation compilation, LintingCode code)
        => [.. Result(compilation).Diagnostics.Where(diagnostic => diagnostic.Code == code)];

    private static SourceLintResult Result(Compilation compilation)
        => new SourceLinter(SourceLinterTestFactory.Finders(), SourceLinterTestFactory.Checkers())
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
