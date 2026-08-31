namespace Forma.Locali.Tests.Cli;

public sealed class CommandLineTests
{
    [Fact]
    public void Should_reject_a_command_line_without_a_path()
    {
        // Act
        var parsed = CommandLine.TryParse([], out _, out var error);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.False(parsed);
            Assert.Equal("'--path' is required.", error);
        });
    }

    [Fact]
    public void Should_reject_a_path_switch_with_no_value()
    {
        // Act
        var parsed = CommandLine.TryParse(["--path", "--report"], out _, out var error);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.False(parsed);
            Assert.Equal("'--path' needs a file or directory.", error);
        });
    }

    [Fact]
    public void Should_reject_an_exclude_switch_with_no_glob()
    {
        // Act
        var parsed = CommandLine.TryParse(["--path", "x", "--exclude"], out _, out var error);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.False(parsed);
            Assert.Equal("'--exclude' needs a glob.", error);
        });
    }

    [Fact]
    public void Should_ask_for_no_exclusions_when_the_switch_is_absent()
    {
        // Act
        _ = CommandLine.TryParse(["--path", "x"], out var options, out _);

        // Assert
        Assert.Empty(options!.Excludes);
    }

    [Fact]
    public void Should_keep_every_exclude_the_command_line_repeats()
    {
        // Act
        _ = CommandLine.TryParse(
            ["--path", "x", "--exclude", "*/Fixtures/*", "--exclude", "*/obj/*"],
            out var options,
            out _);

        // Assert
        Assert.Equal(["*/Fixtures/*", "*/obj/*"], options!.Excludes);
    }

    [Fact]
    public void Should_report_errors_only_when_no_severity_is_named()
    {
        // Act
        _ = CommandLine.TryParse(["--path", "x"], out var options, out _);

        // Assert
        Assert.Equal(LintSeverity.Error, options!.MinimumSeverity);
    }

    [Theory]
    [InlineData("warning", LintSeverity.Warning)]
    [InlineData("WARNING", LintSeverity.Warning)]
    [InlineData("error", LintSeverity.Error)]
    public void Should_read_the_severity_whatever_case_it_is_written_in(string written, LintSeverity expected)
    {
        // Act
        _ = CommandLine.TryParse(["--path", "x", "--severity", written], out var options, out _);

        // Assert
        Assert.Equal(expected, options!.MinimumSeverity);
    }

    [Fact]
    public void Should_reject_a_severity_switch_with_no_level()
    {
        // Act
        var parsed = CommandLine.TryParse(["--path", "x", "--severity"], out _, out var error);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.False(parsed);
            Assert.Equal("'--severity' needs a level.", error);
        });
    }

    [Fact]
    public void Should_reject_a_level_that_is_not_a_severity()
    {
        // Act
        var parsed = CommandLine.TryParse(["--path", "x", "--severity", "loud"], out _, out var error);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.False(parsed);
            Assert.Equal("'loud' is not a severity; use 'error' or 'warning'.", error);
        });
    }

    [Fact]
    public void Should_reject_a_severity_written_as_its_underlying_number()
    {
        // Act
        var parsed = CommandLine.TryParse(["--path", "x", "--severity", "0"], out _, out var error);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.False(parsed);
            Assert.Equal("'0' is not a severity; use 'error' or 'warning'.", error);
        });
    }

    [Fact]
    public void Should_reject_an_argument_the_linter_does_not_take()
    {
        // Act
        var parsed = CommandLine.TryParse(["--path", "x", "--verbose"], out _, out var error);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.False(parsed);
            Assert.Equal("'--verbose' is not an argument the linter takes.", error);
        });
    }

    [Fact]
    public void Should_ask_for_no_report_when_the_switch_is_absent()
    {
        // Act
        _ = CommandLine.TryParse(["--path", "x"], out var options, out _);

        // Assert
        Assert.Null(options!.ReportPath);
    }

    [Fact]
    public void Should_default_the_report_to_the_current_directory()
    {
        // Act
        _ = CommandLine.TryParse(["--path", "x", "--report"], out var options, out _);

        // Assert
        Assert.Equal(Path.GetFullPath("localization-report.json"), options!.ReportPath);
    }

    [Fact]
    public void Should_treat_a_json_report_value_as_the_file_to_write()
    {
        // Act
        _ = CommandLine.TryParse(["--path", "x", "--report", "out/found.json"], out var options, out _);

        // Assert
        Assert.Equal(Path.GetFullPath(Path.Combine("out", "found.json")), options!.ReportPath);
    }

    [Fact]
    public void Should_treat_any_other_report_value_as_the_directory_to_write_into()
    {
        // Act
        _ = CommandLine.TryParse(["--path", "x", "--report", "out"], out var options, out _);

        // Assert
        Assert.Equal(Path.GetFullPath(Path.Combine("out", "localization-report.json")), options!.ReportPath);
    }

    [Fact]
    public void Should_resolve_the_path_against_the_current_directory()
    {
        // Act
        _ = CommandLine.TryParse(["--path", "some/where"], out var options, out _);

        // Assert
        Assert.Equal(Path.GetFullPath(Path.Combine("some", "where")), options!.Path);
    }
}
