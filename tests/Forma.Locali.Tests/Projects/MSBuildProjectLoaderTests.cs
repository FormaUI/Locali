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

    // A pack embeds every locale file under its path, as the runtime's build does, for an assembly the pack itself never declares.
    private const string LocalePack = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <IsLocalePack>true</IsLocalePack>
            <LocalePackCulture>he</LocalePackCulture>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
          </PropertyGroup>
          <ItemGroup>
            <EmbeddedResource Include="Contoso.Widgets\Bars\he.json" WithCulture="false" LogicalName="Contoso.Widgets.Bars.he.json" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public async Task Should_load_a_locale_pack_as_a_pack_rather_than_as_the_owner_of_its_files()
    {
        // Arrange
        using var directory = new TempDirectory();
        directory.Write(Path.Combine("Contoso.Widgets", "Bars", "he.json"), LocalizationFileFakeData.Valid);
        var project = directory.Write("Contoso.Widgets.Locales.Hebrew.csproj", LocalePack);

        // Act
        var (exitCode, output) = await LocaliToolRunner.RunAsync(TestContext.Current.CancellationToken, "--path", project);

        // Assert — the file is embedded, and no type of the pack reads it, which is what a pack is rather than a fault of it.
        Assert.Multiple(
            () => Assert.Equal(0, exitCode),
            () => Assert.DoesNotContain(LintingCodes.UnreachableLocalization.Id, output, StringComparison.Ordinal));
    }

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
