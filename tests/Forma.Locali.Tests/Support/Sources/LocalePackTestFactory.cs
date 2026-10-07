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
        => Resolve(directory, "Hebrew", target, files);

    /// <summary>A pack of the files written under <see cref="Folder"/>, named for the culture given rather than Hebrew, embedded as the runtime's build embeds them, resolved against the target and itself as a whole solution.</summary>
    /// <param name="directory">The test's directory.</param>
    /// <param name="packName">What the pack's own assembly is named, distinguishing it from a sibling pack of another culture in the same test.</param>
    /// <param name="target">The project the pack's folders may name.</param>
    /// <param name="files">The pack's locale files, as full paths under the pack's folder.</param>
    public static LocalePack Resolve(TempDirectory directory, string packName, LoadedProject target, params string[] files)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentException.ThrowIfNullOrEmpty(packName);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(files);

        var packDirectory = Path.Combine(directory.Root, Folder);

        // A pack's build embeds each file under its path with the folders joined by dots.
        var resourceNames = files.ToDictionary(
            Path.GetFullPath,
            file => Path.GetRelativePath(packDirectory, file).Replace(Path.DirectorySeparatorChar, '.').Replace(Path.AltDirectorySeparatorChar, '.'),
            StringComparer.OrdinalIgnoreCase);

        var pack = new LoadedProject(
            Path.Combine(packDirectory, $"Contoso.Locales.{packName}.csproj"),
            CompilationTestFactory.Building($"Contoso.Locales.{packName}"),
            false,
            false,
            EmbeddedResourceNames: resourceNames,
            IsLocalePack: true);

        return LocalePackFiles.Resolve(LocaleFormatTestFactory.Json.RuntimeFormat, pack, files, new LoadedProjects([pack, target], IsWholeSolution: true))!;
    }
}
