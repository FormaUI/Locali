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

    // A pack embeds every locale file under its path, as the runtime's build does, for an assembly the pack itself never declares; this one's folder names no project, which only a solution run can tell.
    private const string LocalePack = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <IsLocalePack>true</IsLocalePack>
            <LocalePackCulture>he</LocalePackCulture>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
          </PropertyGroup>
          <ItemGroup>
            <EmbeddedResource Include="Contoso.Widgetz\Bars\he.json" WithCulture="false" LogicalName="Contoso.Widgetz.Bars.he.json" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public async Task Should_load_a_locale_pack_as_a_pack_rather_than_as_the_owner_of_its_files()
    {
        // Arrange
        using var directory = new TempDirectory();
        var project = WritePack(directory);

        // Act
        var (exitCode, output) = await LocaliToolRunner.RunAsync(TestContext.Current.CancellationToken, "--path", project);

        // Assert — the file is embedded, and no type of the pack reads it, which is what a pack is rather than a fault of it.
        Assert.Multiple(
            () => Assert.Equal(0, exitCode),
            () => Assert.DoesNotContain(LintingCodes.UnreachableLocalization.Id, output, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_report_a_pack_folder_naming_no_project_of_a_solution()
    {
        // Arrange — the solution lists the pack alone, so the folder names an assembly built nowhere it reaches.
        using var directory = new TempDirectory();
        WritePack(directory);
        var solution = directory.Write("Scratch.slnx", """
            <Solution>
              <Project Path="Contoso.Widgets.Locales.Hebrew.csproj" />
            </Solution>
            """);

        // Act
        var (exitCode, output) = await LocaliToolRunner.RunAsync(TestContext.Current.CancellationToken, "--path", solution);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(1, exitCode),
            () => Assert.Contains(LintingCodes.UnknownLocalePackAssembly.Id, output, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_stand_a_project_in_another_language_in_for_what_it_builds()
    {
        // Arrange — a Visual Basic project may build the assembly the folder names, and the linter cannot read it to say.
        using var directory = new TempDirectory();
        WritePack(directory);
        directory.Write(Path.Combine("Contoso.Widgetz", "Contoso.Widgetz.vbproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");
        var solution = directory.Write("Scratch.slnx", """
            <Solution>
              <Project Path="Contoso.Widgets.Locales.Hebrew.csproj" />
              <Project Path="Contoso.Widgetz/Contoso.Widgetz.vbproj" />
            </Solution>
            """);

        // Act
        var (exitCode, output) = await LocaliToolRunner.RunAsync(TestContext.Current.CancellationToken, "--path", solution);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(0, exitCode),
            () => Assert.DoesNotContain(LintingCodes.UnknownLocalePackAssembly.Id, output, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_hold_a_packs_file_to_the_types_of_the_project_its_folder_names()
    {
        // Arrange — the pack names a project of the solution, so its file's sections are held to that project's types.
        using var directory = new TempDirectory();
        directory.Write(Path.Combine("Widgets", "Widget.cs"), "namespace Contoso.Widgets; public sealed class Widget;");
        directory.Write(Path.Combine("Widgets", "Contoso.Widgets.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        var file = directory.Write(Path.Combine("Packs", "Contoso.Widgets", "he.json"), LocalizationFileFakeData.WithSections(("Widget", "Today", "היום"), ("Gadget", "Today", "היום")));
        directory.Write(Path.Combine("Packs", "Contoso.Widgets.Locales.Hebrew.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <IsLocalePack>true</IsLocalePack>
                <LocalePackCulture>he</LocalePackCulture>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>
              <ItemGroup>
                <EmbeddedResource Include="Contoso.Widgets\he.json" WithCulture="false" LogicalName="Contoso.Widgets.he.json" />
              </ItemGroup>
            </Project>
            """);
        var solution = directory.Write("Scratch.slnx", """
            <Solution>
              <Project Path="Widgets/Contoso.Widgets.csproj" />
              <Project Path="Packs/Contoso.Widgets.Locales.Hebrew.csproj" />
            </Solution>
            """);

        // Act
        var (exitCode, output) = await LocaliToolRunner.RunAsync(TestContext.Current.CancellationToken, "--path", solution);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(1, exitCode),
            () => Assert.Contains($"{file}(1,1): error {LintingCodes.UnreachableLocalization.Id}: Section 'Gadget'", output, StringComparison.Ordinal),
            () => Assert.DoesNotContain(LintingCodes.UnknownLocalePackAssembly.Id, output, StringComparison.Ordinal));
    }

    private static string WritePack(TempDirectory directory)
    {
        directory.Write(Path.Combine("Contoso.Widgetz", "Bars", "he.json"), LocalizationFileFakeData.Valid);

        return directory.Write("Contoso.Widgets.Locales.Hebrew.csproj", LocalePack);
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
