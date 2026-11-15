namespace Forma.Locali.Documents;

using Forma.Framework.Localization.Files;

/// <summary>Reads a locale file's name as the runtime's default reading does, into the culture it is for and the parts of the name it stands for.</summary>
public static class LocalizationFileName
{
    /// <summary>What the runtime's default reading takes from a name, or null where the name carries no culture.</summary>
    /// <param name="name">The file's name from where its source's names begin, with folders written with <c>/</c>, <c>\</c> or <c>.</c>.</param>
    /// <param name="extension">The extension the format's files carry.</param>
    /// <param name="localesDir">The folder of locale files the reading leaves out, written with <c>/</c>.</param>
    public static LocaleFileName? Read(string name, string extension, string localesDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(extension);
        ArgumentException.ThrowIfNullOrEmpty(localesDir);

        return LocaleFileNames.TryRead(name.Replace('\\', '/'), localesDir, extension, StringComparison.Ordinal, out var fileName)
            ? fileName
            : null;
    }

    /// <summary>What the runtime's default reading takes from a file's path from its project's folder, or null where the name carries no culture.</summary>
    /// <param name="path">The file.</param>
    /// <param name="projectPath">The project the file belongs to, or null where none sits above it.</param>
    /// <param name="extension">The extension the format's files carry.</param>
    /// <param name="localesDir">The folder of locale files the project's names are read without, written with <c>/</c>.</param>
    /// <remarks>
    /// A file in the <c>.locales</c> folder beside the project that the project's folder of locale files reads no culture from
    /// is read as the host reads its own folder, without <c>.locales</c>. A file no project sits above is read from its first
    /// <c>.locales</c> folder, or by its name alone where it sits in none.
    /// </remarks>
    public static LocaleFileName? ReadInProject(string path, string? projectPath, string extension, string localesDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(localesDir);

        var fullPath = Path.GetFullPath(path);

        // A folder above the project is no part of any name the runtime reads, and may itself spell a culture.
        if (projectPath is not null)
        {
            var name = Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(projectPath))!, fullPath);
            var isInHostFolder = name.StartsWith(LocaleConfig.DefaultLocalesDir + Path.DirectorySeparatorChar, StringComparison.Ordinal);

            return Read(name, extension, localesDir) ?? (isInHostFolder ? Read(name, extension, LocaleConfig.DefaultLocalesDir) : null);
        }

        var segments = fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var localesDirPosition = Array.IndexOf(segments, LocaleConfig.DefaultLocalesDir);

        return Read(localesDirPosition < 0 ? segments[^1] : string.Join('/', segments[localesDirPosition..]), extension, LocaleConfig.DefaultLocalesDir);
    }
}
