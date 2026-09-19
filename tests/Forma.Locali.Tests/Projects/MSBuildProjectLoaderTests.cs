namespace Forma.Locali.Tests.Projects;

using Forma.Locali.Tests.Support.Cli;

public sealed class MSBuildProjectLoaderTests
{
    // The locale file is embedded only under Release, and no type is named for its folder, so LOC0112 speaks exactly when the load sees it embedded.
    private const string Project = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <EmbeddedResource Include="Shipped\.locales\*.json" WithCulture="false" Condition="'$(Configuration)' == 'Release'" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public async Task Should_load_a_project_as_its_default_build_when_no_property_is_named()
    {
        // Arrange
        using var directory = new TempDirectory();
        var project = Write(directory);

        // Act
        var (exitCode, output) = await LocaliToolRunner.RunAsync(TestContext.Current.CancellationToken, "--path", project);

        // Assert — the file is not embedded under the default build, so it reads as a host's and no type-bound rule judges it.
        Assert.Multiple(
            () => Assert.Equal(0, exitCode),
            () => Assert.DoesNotContain(LintingCodes.UnreachableLocalization.Id, output, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_load_a_project_with_the_properties_the_command_line_names()
    {
        // Arrange
        using var directory = new TempDirectory();
        var project = Write(directory);

        // Act
        var (exitCode, output) = await LocaliToolRunner.RunAsync(TestContext.Current.CancellationToken, "--path", project, "--property", "Configuration=Release");

        // Assert — under Release the file is embedded, and nothing reads it.
        Assert.Multiple(
            () => Assert.Equal(1, exitCode),
            () => Assert.Contains(LintingCodes.UnreachableLocalization.Id, output, StringComparison.Ordinal));
    }

    private static string Write(TempDirectory directory)
    {
        directory.Write("Widget.cs", "namespace Scratch; public sealed class Widget;");
        directory.Write(Path.Combine("Shipped", ".locales", "he.json"), LocalizationFileFakeData.Valid);

        return directory.Write("Scratch.csproj", Project);
    }
}
