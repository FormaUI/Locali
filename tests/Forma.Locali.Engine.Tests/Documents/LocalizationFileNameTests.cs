namespace Forma.Locali.Tests.Documents;

public sealed class LocalizationFileNameTests
{
    [Theory]
    [InlineData("he.json", "he")]
    [InlineData("es-419.json", "es-419")]
    [InlineData("Colocated.he.json", "he")]
    [InlineData(".locales/es/Accounts.Admin.json", "es")]
    [InlineData("Components/Bars/.locales/he.json", "he")]
    [InlineData("Scratch.Components..locales.he.json", "he")]
    [InlineData("appsettings.json", null)]
    [InlineData("Colocated.zzz.json", null)]
    [InlineData("He.json", null)]
    [InlineData(".locales/ES/Accounts.json", null)]
    [InlineData("root.json", null)]
    public void Should_read_the_culture_out_of_a_files_name(string name, string? expected)
    {
        // Act
        var fileName = LocalizationFileName.Read(name, ".json", ".locales");

        // Assert
        Assert.Equal(expected, fileName?.Culture);
    }

    [Theory]
    [InlineData(".locales/es/Accounts.Admin.json", "Accounts.Admin")]
    [InlineData(".locales/es/Accounts/Admin.json", "Accounts.Admin")]
    [InlineData("Accounts/Admin/.locales/es.json", "Accounts.Admin")]
    [InlineData("Accounts.Admin.es.json", "Accounts.Admin")]
    [InlineData(@"Accounts\Admin\.locales\es.json", "Accounts.Admin")]
    public void Should_read_folders_and_dots_alike_into_the_name_a_file_stands_for(string name, string expected)
    {
        // Act
        var fileName = LocalizationFileName.Read(name, ".json", ".locales");

        // Assert
        Assert.Equal(expected, string.Join('.', fileName!.Parts));
    }

    [Fact]
    public void Should_tell_a_file_in_a_folder_of_locale_files_from_one_outside_any()
    {
        // Act
        var inside = LocalizationFileName.Read("Components/.locales/he.json", ".json", ".locales");
        var outside = LocalizationFileName.Read("Components/he.json", ".json", ".locales");

        // Assert
        Assert.Multiple(
            () => Assert.True(inside!.IsInLocalesDir),
            () => Assert.False(outside!.IsInLocalesDir));
    }

    [Fact]
    public void Should_read_a_files_name_from_its_projects_folder()
    {
        // Arrange — a folder above the project spells a culture the runtime never sees.
        var projectPath = Path.Combine(Path.GetTempPath(), "es", "Scratch", "Scratch.csproj");
        var path = Path.Combine(Path.GetTempPath(), "es", "Scratch", "Components", "Strings.json");

        // Act
        var fileName = LocalizationFileName.ReadInProject(path, projectPath, ".json");

        // Assert
        Assert.Null(fileName);
    }

    [Fact]
    public void Should_read_a_files_name_alone_where_no_project_sits_above_it()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "es", "Strings.he.json");

        // Act
        var fileName = LocalizationFileName.ReadInProject(path, projectPath: null, ".json");

        // Assert
        Assert.Equal("he", fileName?.Culture);
    }

    [Fact]
    public void Should_read_a_files_name_from_its_first_folder_of_locale_files_where_no_project_sits_above_it()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "my", "Widgets", ".locales", "es", "Components.json");

        // Act
        var fileName = LocalizationFileName.ReadInProject(path, projectPath: null, ".json");

        // Assert
        Assert.Multiple(
            () => Assert.Equal("es", fileName?.Culture),
            () => Assert.True(fileName?.IsInLocalesDir));
    }
}
