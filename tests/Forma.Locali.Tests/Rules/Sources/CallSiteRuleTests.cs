namespace Forma.Locali.Tests.Rules.Sources;

public sealed class CallSiteRuleTests
{
    [Fact]
    public void Should_report_fewer_values_than_the_key_names_holes()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["Stored {stored} of {total}.", 1];""");

        // Assert
        Assert.Equal([LintingCodes.ValueCountMismatch], diagnostics.Codes());
    }

    [Fact]
    public void Should_report_more_values_than_a_key_naming_no_hole_takes()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint("""public string Read() => locale["Saved.", 1, 2];""");

        // Assert
        Assert.Equal([LintingCodes.ValueCountMismatch], diagnostics.Codes());
    }

    [Fact]
    public void Should_accept_one_value_for_each_hole_the_key_names()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["Stored {stored} of {total}.", 1, 2];""");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_count_a_repeated_hole_once()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["{name} and {name}", 1];""");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_an_interpolated_key_carrying_a_hole()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() { var count = 1; return locale[$"Stored {count}"]; }""");

        // Assert
        Assert.Equal([LintingCodes.InterpolatedKey], diagnostics.Codes());
    }

    [Fact]
    public void Should_report_an_expression_reading_a_name_the_key_names_no_hole_for()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["[$ [count] plural; one: # file; other: # files; $]"];""");

        // Assert
        Assert.Equal([LintingCodes.UnsuppliedPluralArgument], diagnostics.Codes());
    }

    [Fact]
    public void Should_accept_an_expression_reading_a_hole_the_key_names()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["{count} [$ [count] plural; one: file; other: files; $]", 1];""");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_an_expression_naming_no_argument_when_no_value_is_supplied()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["[$ plural; one: # file; other: # files; $]"];""");

        // Assert
        Assert.Equal([LintingCodes.SoleArgumentExpected], diagnostics.Codes());
    }

    [Fact]
    public void Should_accept_an_expression_naming_no_argument_when_one_value_is_supplied()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["[$ plural; one: # file; other: # files; $]", 1];""");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_plural_expression_that_omits_the_fallback_category()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["{count} [$ [count] plural; one: file; $]", 1];""");

        // Assert
        Assert.Equal([LintingCodes.MissingPluralOther], diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_an_interpolated_key_with_no_holes_to_the_compiler_to_fold()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint("""public string Read() => locale[$"Use {{curly}} braces"];""");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_leave_a_lookup_that_names_only_a_key_alone()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint("""public string Read() => locale["Saved."];""");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_count_a_tuple_value_as_one_value()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["Stored {stored} of {total}.", ("stored", (object?)1)];""");

        // Assert
        Assert.Equal([LintingCodes.ValueCountMismatch], diagnostics.Codes());
    }

    [Fact]
    public void Should_not_count_a_values_collection_passed_whole_as_one_value()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() { object?[] values = [1, 2]; return locale["Stored {stored} of {total}.", values]; }""");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_still_count_a_value_that_is_an_array_of_another_element_type()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() { int[] values = [1, 2]; return locale["Stored {stored} of {total}.", values]; }""");

        // Assert
        Assert.Equal([LintingCodes.ValueCountMismatch], diagnostics.Codes());
    }

    [Theory]
    [InlineData("Value: {0}")]
    [InlineData("Value: {0,5}")]
    [InlineData("Value: {1:N0}")]
    [InlineData("Value: {items.Length}")]
    [InlineData("Value: {a-b}")]
    [InlineData("Value: {stored,x}")]
    [InlineData("Value: {stored,x:N0}")]
    public void Should_report_a_key_writing_something_shaped_like_a_hole_it_cannot_read(string key)
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint($"""public string Read() => locale["{key}"];""");

        // Assert
        Assert.Equal([LintingCodes.HoleShapedText], diagnostics.Codes());
    }

    [Theory]
    [InlineData("Use {{curly}} braces")]
    [InlineData("Set body { color: red } here")]
    [InlineData("An empty { } pair")]
    [InlineData("Nothing at all")]
    [InlineData("Pick {colour, red} or blue")]
    public void Should_leave_a_key_the_runtime_reads_as_prose_alone(string key)
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint($"""public string Read() => locale["{key}"];""");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("Value: {stored}")]
    [InlineData("Value: {stored,5}")]
    [InlineData("Value: {stored,-5}")]
    [InlineData("Value: {stored:N0}")]
    [InlineData("Value: {stored,5:N0}")]
    public void Should_leave_a_hole_the_runtime_reads_alone(string key)
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint($"""public string Read() => locale["{key}", 1];""");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("""public string Read() => locale["{count}", 1];""")]
    [InlineData("""public string Read() => locale["{count} / {total}", 1, 2];""")]
    [InlineData("""public string Read() => locale["{count}: {total}", 1, 2];""")]
    public void Should_report_a_key_carrying_holes_and_no_words(string source)
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(source);

        // Assert
        Assert.Equal([LintingCodes.BareHoleKey], diagnostics.Codes());
    }

    [Theory]
    [InlineData("""public string Read() => locale["Stored {stored} of {total}.", 1, 2];""")]
    [InlineData("""public string Read() => locale["Saved."];""")]
    [InlineData("""public string Read() => locale["{count} files", 1];""")]
    public void Should_leave_a_key_carrying_words_alone(string source)
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(source);

        // Assert
        Assert.DoesNotContain(LintingCodes.BareHoleKey, diagnostics.Codes());
    }

    [Fact]
    public void Should_report_an_expression_key_carrying_no_words_in_any_field()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["[$ plural; other: #; $]", 1];""");

        // Assert
        Assert.Equal([LintingCodes.BareExpressionKey], diagnostics.Codes());
    }

    [Theory]
    [InlineData("""public string Read() => locale[" [$ plural; other: #; $] ", 1];""")]
    [InlineData("""public string Read() => locale["([$ plural; other: #; $])", 1];""")]
    public void Should_report_an_expression_key_padded_with_no_words(string source)
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(source);

        // Assert
        Assert.Equal([LintingCodes.BareExpressionKey], diagnostics.Codes());
    }

    [Fact]
    public void Should_report_a_key_of_two_expressions_carrying_no_words()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["[$ plural; other: #; $] / [$ plural; other: #; $]", 1];""");

        // Assert
        Assert.Equal([LintingCodes.BareExpressionKey], diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_an_expression_key_whose_fields_carry_words_alone()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["[$ plural; one: # file; other: # files; $]", 1];""");

        // Assert
        Assert.DoesNotContain(LintingCodes.BareExpressionKey, diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_an_expression_key_alone_when_prose_sits_outside_it()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["Found: [$ [count] plural; other: #; $]", 1];""");

        // Assert
        Assert.DoesNotContain(LintingCodes.BareExpressionKey, diagnostics.Codes());
    }

    [Theory]
    [InlineData("$import")]
    [InlineData("$shraed")]
    [InlineData("$x")]
    public void Should_report_a_key_shaped_like_a_reserved_entry(string key)
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint($"""public string Read() => locale["{key}"];""");

        // Assert
        Assert.Equal([LintingCodes.ReservedShapedLookup], diagnostics.Codes());
    }

    [Theory]
    [InlineData("$")]
    [InlineData("$ per month")]
    [InlineData("$5 off")]
    [InlineData("$0.00")]
    [InlineData("$USD")]
    [InlineData("$camelCase")]
    [InlineData("$_private")]
    [InlineData("$tools.x")]
    [InlineData("$import something")]
    [InlineData("$price/month")]
    [InlineData("Costs $5")]
    public void Should_leave_a_key_that_is_not_one_lowercase_word_alone(string key)
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint($"""public string Read() => locale["{key}"];""");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("$$price")]
    [InlineData("$$import")]
    public void Should_leave_an_escaped_key_alone(string key)
    {
        // Act — a doubled sign keeps the rest as text.
        var diagnostics = SourceLinterTestFactory.Lint($"""public string Read() => locale["{key}"];""");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_a_lookup_passed_to_string_format()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Show() => string.Format(locale["Stored {Count} of {Total}."], 1, 2);""");

        // Assert
        Assert.Contains(LintingCodes.ReformattedLookup, diagnostics.Codes());
    }

    [Fact]
    public void Should_report_a_lookup_passed_to_string_format_with_a_culture_provider()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Show() => string.Format(System.Globalization.CultureInfo.InvariantCulture, locale["Stored {Count} of {Total}."], 1, 2);""");

        // Assert
        Assert.Contains(LintingCodes.ReformattedLookup, diagnostics.Codes());
    }

    [Fact]
    public void Should_report_a_lookup_carrying_its_own_values_when_still_passed_to_string_format()
    {
        // Act — the lookup already fills its own holes; string.Format still reformats the result.
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Show() => string.Format(locale["Stored {Count} of {Total}.", 1, 2]);""");

        // Assert
        Assert.Contains(LintingCodes.ReformattedLookup, diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_a_lookup_that_is_not_passed_to_string_format_alone()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Show() => locale["Stored {Count} of {Total}.", 1, 2];""");

        // Assert
        Assert.DoesNotContain(LintingCodes.ReformattedLookup, diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_a_lookup_passed_to_an_unrelated_method_alone()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint("""public string Show() => string.Concat(locale["Saved."], "!");""");

        // Assert
        Assert.DoesNotContain(LintingCodes.ReformattedLookup, diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_a_lookup_supplied_as_a_replacement_value_alone()
    {
        // Act — the lookup fills a hole in someone else's format text; it does not supply the format text itself.
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Show() => string.Format("Searched for {0}.", locale["Query"]);""");

        // Assert
        Assert.DoesNotContain(LintingCodes.ReformattedLookup, diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_a_lookup_supplied_as_a_named_replacement_value_alone()
    {
        // Act — a named argument puts the lookup first in source, but it still binds to the value parameter.
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Show() => string.Format(arg0: locale["Query"], format: "Searched for {0}.");""");

        // Assert
        Assert.DoesNotContain(LintingCodes.ReformattedLookup, diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_a_lookup_supplied_as_one_of_several_replacement_values_alone()
    {
        // Act — four values force the params overload; the lookup sits inside the expanded array, not the format text.
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Show() => string.Format("Values: {0}, {1}, {2}, {3}", 1, 2, 3, locale["Query"]);""");

        // Assert
        Assert.DoesNotContain(LintingCodes.ReformattedLookup, diagnostics.Codes());
    }
}
