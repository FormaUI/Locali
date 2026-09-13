namespace Forma.Locali.Tests.Diagnostics;

public sealed class LintingCodesTests
{
    [Fact]
    public void Should_give_every_rule_a_distinct_identifier()
    {
        // Act
        var ids = LintingCodes.All.Select(code => code.Id).Distinct(StringComparer.Ordinal);

        // Assert
        Assert.Equal(LintingCodes.All.Count, ids.Count());
    }

    [Fact]
    public void Should_describe_every_rule()
    {
        // Assert
        Assert.All(LintingCodes.All, code => Assert.False(string.IsNullOrWhiteSpace(code.Description)));
    }

    [Fact]
    public void Should_catalogue_the_code_every_shipped_file_rule_reports_under()
    {
        // Act
        var codes = SchemaLinterTestFactory.Checkers().Select(checker => checker.Code);

        // Assert
        Assert.All(codes, code => Assert.Contains(code, LintingCodes.All));
    }

    [Fact]
    public void Should_catalogue_the_code_every_shipped_call_site_rule_reports_under()
    {
        // Act
        var codes = SourceLinterTestFactory.Checkers().Select(checker => checker.Code);

        // Assert
        Assert.All(codes, code => Assert.Contains(code, LintingCodes.All));
    }

    [Fact]
    public void Should_let_no_two_file_rules_report_under_the_same_code()
    {
        // Act
        var codes = SchemaLinterTestFactory.Checkers().Select(checker => checker.Code).ToArray();

        // Assert
        Assert.Equal(codes.Length, codes.Distinct().Count());
    }

    [Fact]
    public void Should_let_no_two_call_site_rules_report_under_the_same_code()
    {
        // Act
        var codes = SourceLinterTestFactory.Checkers().Select(checker => checker.Code).ToArray();

        // Assert
        Assert.Equal(codes.Length, codes.Distinct().Count());
    }

    [Fact]
    public void Should_number_every_catalogued_code_in_a_family_range()
    {
        // Act
        var ids = LintingCodes.All.Select(code => code.Id);

        // Assert
        Assert.All(ids, id => Assert.True(
            id.StartsWith("LOC00", StringComparison.Ordinal)
                || id.StartsWith("LOC01", StringComparison.Ordinal)
                || id.StartsWith("LOC02", StringComparison.Ordinal),
            $"'{id}' is in no family range: linting configuration, file, or call-site."));
    }

    [Fact]
    public void Should_report_every_file_rule_in_the_100s()
    {
        // Act
        var codes = SchemaLinterTestFactory.Checkers().Select(checker => checker.Code.Id);

        // Assert
        Assert.All(codes, id => Assert.StartsWith("LOC01", id, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_report_every_call_site_rule_in_the_200s()
    {
        // Act
        var codes = SourceLinterTestFactory.Checkers().Select(checker => checker.Code.Id);

        // Assert
        Assert.All(codes, id => Assert.StartsWith("LOC02", id, StringComparison.Ordinal));
    }
}
