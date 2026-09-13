namespace Forma.Locali.Tests.Linting;

using Forma.Locali.Linting;

public sealed class LocalizationFileLinterTests
{
    [Fact]
    public async Task Should_report_a_rule_the_file_ignores_and_say_so_separately()
    {
        // Arrange — two sections translate one key two ways, which the file says is meant.
        using var directory = new TempDirectory();
        var file = directory.Write("Widgets.he.json", LocalizationFileFakeData.WithIgnore(["LOC0113"], ("Widget", "Today", "היום"), ("Gallery", "Today", "עכשיו")));

        // Act
        var result = await new LocalizationFileLinter(SchemaLinterTestFactory.Create()).LintAsync(file, TestContext.Current.CancellationToken);

        // Assert — the file's own report still carries the diagnostic; a caller decides whether to show it.
        Assert.Multiple(
            () => Assert.Contains(LintingCodes.InconsistentTranslation, result.Diagnostics.Codes()),
            () => Assert.True(result.Ignored.Ignores(LintingCodes.InconsistentTranslation)));
    }

    [Fact]
    public async Task Should_leave_a_rule_the_file_never_names_unignored()
    {
        // Arrange
        using var directory = new TempDirectory();
        var file = directory.Write("Widgets.he.json", LocalizationFileFakeData.Valid);

        // Act
        var result = await new LocalizationFileLinter(SchemaLinterTestFactory.Create()).LintAsync(file, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Ignored.Ignores(LintingCodes.InconsistentTranslation));
    }

    [Fact]
    public async Task Should_still_judge_the_tools_entry_itself()
    {
        // Arrange — a file cannot ignore the rule over its own tools entry.
        using var directory = new TempDirectory();
        var file = directory.Write("Widgets.he.json", LocalizationFileFakeData.WithIgnore(["LOC0114", "nonsense"], ("Widget", "Today", "היום")));

        // Act
        var result = await new LocalizationFileLinter(SchemaLinterTestFactory.Create()).LintAsync(file, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(LintingCodes.MalformedTools, result.Diagnostics.Codes());
    }
}
