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
        Assert.Equal([LintCodes.ValueCountMismatch], diagnostics.Codes());
    }

    [Fact]
    public void Should_report_more_values_than_a_key_naming_no_hole_takes()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint("""public string Read() => locale["Saved.", 1, 2];""");

        // Assert
        Assert.Equal([LintCodes.ValueCountMismatch], diagnostics.Codes());
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
    public void Should_report_an_interpolated_hole_that_is_not_a_plain_identifier()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() { var items = new[] { 1 }; return locale[$"Stored {items.Length}"]; }""");

        // Assert
        Assert.Equal([LintCodes.HoleNotIdentifier], diagnostics.Codes());
    }

    [Fact]
    public void Should_accept_an_interpolated_hole_that_is_a_plain_identifier()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() { var count = 1; return locale[$"Stored {count}"]; }""");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_an_expression_reading_a_name_the_key_names_no_hole_for()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["[$ [count] plural; one: # file; other: # files; $]"];""");

        // Assert
        Assert.Equal([LintCodes.UnsuppliedPluralArgument], diagnostics.Codes());
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
        Assert.Equal([LintCodes.SoleArgumentExpected], diagnostics.Codes());
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
        Assert.Equal([LintCodes.MissingPluralOther], diagnostics.Codes());
    }

    [Fact]
    public void Should_report_a_malformed_expression_in_an_interpolated_key()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() { var count = 1; return locale[$"{count} [$ [count] plural one: file; $]"]; }""");

        // Assert
        Assert.Equal([LintCodes.MalformedSourceExpression], diagnostics.Codes());
    }

    [Fact]
    public void Should_read_an_interpolated_key_as_the_text_the_handler_rebuilds()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() { var count = 1; return locale[$"{count} [$ [count] plural; one: file; $]"]; }""");

        // Assert
        Assert.Equal([LintCodes.MissingPluralOther], diagnostics.Codes());
    }

    [Fact]
    public void Should_treat_doubled_braces_in_an_interpolated_key_as_literal_text()
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
    public void Should_not_count_values_that_carry_their_own_names()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["Stored {stored} of {total}.", ("stored", (object?)1)];""");

        // Assert
        Assert.Empty(diagnostics);
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
        Assert.Equal([LintCodes.ValueCountMismatch], diagnostics.Codes());
    }

    [Fact]
    public void Should_report_a_named_value_the_key_never_reads()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["Stored {stored} of {total}.", ("stord", (object?)1), ("total", (object?)2)];""");

        // Assert
        Assert.Equal([LintCodes.UnreadValue], diagnostics.Codes());
    }

    [Fact]
    public void Should_accept_named_values_the_key_reads()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["Stored {stored} of {total}.", ("stored", (object?)1), ("total", (object?)2)];""");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_accept_a_named_value_an_expression_reads()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["[$ [count] plural; one: # file; other: # files; $]", ("count", (object?)1)];""");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Should_report_an_expression_reading_a_name_the_named_values_miss()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["[$ [count] plural; one: # file; other: # files; $]", ("cont", (object?)1)];""");

        // Assert
        Assert.Equal(
            [LintCodes.UnsuppliedPluralArgument, LintCodes.UnreadValue],
            diagnostics.Codes().OrderBy(code => code.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void Should_report_an_expression_reading_a_name_an_interpolated_key_misses()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() { var count = 1; return locale[$"{count} [$ [cont] plural; one: file; other: files; $]"]; }""");

        // Assert
        Assert.Equal([LintCodes.UnsuppliedPluralArgument], diagnostics.Codes());
    }

    [Fact]
    public void Should_judge_no_names_it_cannot_read()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """
            public string Read()
            {
                var pair = ("stord", (object?)1);

                return locale["Stored {stored} of {total}.", pair];
            }
            """);

        // Assert
        Assert.Empty(diagnostics);
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
        Assert.Equal([LintCodes.HoleShapedText], diagnostics.Codes());
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

    [Fact]
    public void Should_leave_an_interpolated_hole_to_the_rule_that_judges_it_as_code()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() { var items = new[] { 1 }; return locale[$"Stored {items.Length}"]; }""");

        // Assert
        Assert.Equal([LintCodes.HoleNotIdentifier], diagnostics.Codes());
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
        Assert.Equal([LintCodes.BareHoleKey], diagnostics.Codes());
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
        Assert.DoesNotContain(LintCodes.BareHoleKey, diagnostics.Codes());
    }

    [Fact]
    public void Should_report_an_expression_key_carrying_no_words_in_any_field()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["[$ plural; other: #; $]", 1];""");

        // Assert
        Assert.Equal([LintCodes.BareExpressionKey], diagnostics.Codes());
    }

    [Theory]
    [InlineData("""public string Read() => locale[" [$ plural; other: #; $] ", 1];""")]
    [InlineData("""public string Read() => locale["([$ plural; other: #; $])", 1];""")]
    public void Should_report_an_expression_key_padded_with_no_words(string source)
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(source);

        // Assert
        Assert.Equal([LintCodes.BareExpressionKey], diagnostics.Codes());
    }

    [Fact]
    public void Should_report_a_key_of_two_expressions_carrying_no_words()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["[$ [a] plural; other: #; $] / [$ [b] plural; other: #; $]", ("a", (object?)1), ("b", (object?)2)];""");

        // Assert
        Assert.Equal([LintCodes.BareExpressionKey], diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_an_expression_key_whose_fields_carry_words_alone()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["[$ plural; one: # file; other: # files; $]", 1];""");

        // Assert
        Assert.DoesNotContain(LintCodes.BareExpressionKey, diagnostics.Codes());
    }

    [Fact]
    public void Should_leave_an_expression_key_alone_when_prose_sits_outside_it()
    {
        // Act
        var diagnostics = SourceLinterTestFactory.Lint(
            """public string Read() => locale["Found: [$ [count] plural; other: #; $]", ("count", (object?)1)];""");

        // Assert
        Assert.DoesNotContain(LintCodes.BareExpressionKey, diagnostics.Codes());
    }
}
