namespace Forma.Locali.Tests.Linting;

using Forma.Locali.Linting;

public sealed class LocalizationFileLinterTests
{
    [Fact]
    public async Task Should_leave_a_rule_the_file_ignores_out_of_its_report()
    {
        // Arrange — two sections translate one key two ways, which the file says is meant.
        using var directory = new TempDirectory();
        var file = directory.Write("Widgets.he.json", LocalizationFileFakeData.WithIgnore(["LOC0113"], ("Widget", "Today", "היום"), ("Gallery", "Today", "עכשיו")));

        // Act
        var diagnostics = await new LocalizationFileLinter(SchemaLinterTestFactory.Create()).LintAsync(file, TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(LintCodes.InconsistentTranslation, diagnostics.Codes());
    }

    [Fact]
    public async Task Should_still_judge_the_tools_entry_itself()
    {
        // Arrange — a file cannot ignore the rule over its own tools entry.
        using var directory = new TempDirectory();
        var file = directory.Write("Widgets.he.json", LocalizationFileFakeData.WithIgnore(["LOC0114", "nonsense"], ("Widget", "Today", "היום")));

        // Act
        var diagnostics = await new LocalizationFileLinter(SchemaLinterTestFactory.Create()).LintAsync(file, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(LintCodes.MalformedTools, diagnostics.Codes());
    }
}
