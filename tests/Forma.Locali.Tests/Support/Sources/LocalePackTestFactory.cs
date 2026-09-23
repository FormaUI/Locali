namespace Forma.Locali.Tests.Support.Sources;

using Forma.Locali.Projects;
using Forma.Locali.Sources;

/// <summary>Builds a locale pack in a test's directory, resolved against the project it localizes as the linter resolves one.</summary>
public static class LocalePackTestFactory
{
    /// <summary>The folder beneath a test's directory a pack's files are written under, a folder per assembly inside it.</summary>
    public const string Folder = "Packs";

    /// <summary>A pack of the files written under <see cref="Folder"/>, embedded as the runtime's build embeds them, resolved against the target and itself as a whole solution.</summary>
    /// <param name="directory">The test's directory.</param>
    /// <param name="target">The project the pack's folders may name.</param>
    /// <param name="files">The pack's locale files, as full paths under the pack's folder.</param>
    public static LocalePack Resolve(TempDirectory directory, LoadedProject target, params string[] files)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(files);

        var packDirectory = Path.Combine(directory.Root, Folder);

        // A pack's build embeds each file under its path with the folders joined by dots.
        var resourceNames = files.ToDictionary(
            Path.GetFullPath,
            file => Path.GetRelativePath(packDirectory, file).Replace(Path.DirectorySeparatorChar, '.').Replace(Path.AltDirectorySeparatorChar, '.'),
            StringComparer.OrdinalIgnoreCase);

        var pack = new LoadedProject(
            Path.Combine(packDirectory, "Contoso.Locales.Hebrew.csproj"),
            CompilationTestFactory.Building("Contoso.Locales.Hebrew"),
            false,
            false,
            EmbeddedResourceNames: resourceNames,
            IsLocalePack: true);

        return LocalePackFiles.Resolve(pack, files, new LoadedProjects([pack, target], IsWholeSolution: true))!;
    }
}
