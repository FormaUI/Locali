namespace Forma.Locali.Tests.Documents;

public sealed class LocalizationFileNameTests
{
    [Theory]
    [InlineData("Colocated.he.json", "he")]
    [InlineData("Colocated.es-419.json", "es-419")]
    [InlineData("appsettings.json", null)]
    [InlineData("Colocated.zzz.json", null)]
    public void Should_read_the_culture_out_of_a_file_name(string name, string? expected)
    {
        // Act
        var culture = LocalizationFileName.CultureOf(name);

        // Assert
        Assert.Equal(expected, culture);
    }
}
