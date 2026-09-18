namespace Forma.Locali.Sources;

using Forma.Framework.Localization.Files;

/// <summary>A project's translation files, read once from its source tree and served under the names the build embeds them by, so the runtime's own walk finds them as it would in the built assembly.</summary>
public sealed class ProjectLocaleSource : LocaleSource
{
    private readonly Dictionary<string, TranslationFile> _files = new(StringComparer.Ordinal);

    /// <summary>Indexes the files by the name the build embeds each under.</summary>
    /// <param name="files">The project's translation files.</param>
    /// <param name="format">The format the files are written in.</param>
    public ProjectLocaleSource(IReadOnlyList<TranslationFile> files, ILocaleFileFormat format)
        : base(format)
    {
        ArgumentNullException.ThrowIfNull(files);

        foreach (var file in files)
        {
            _files[file.Name] = file;
        }
    }

    /// <summary>The file the source serves under a name, or null when none answers to it.</summary>
    /// <param name="name">The name, as the source's entries report it.</param>
    public TranslationFile? GetFile(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return _files.GetValueOrDefault(name);
    }

    /// <summary>The full name of every type or namespace the runtime reads a file for, none when no path in force reads it.</summary>
    /// <param name="assemblyName">The name of the assembly the project builds.</param>
    /// <param name="file">The file.</param>
    public IReadOnlyList<string> GetAnchorNames(string assemblyName, TranslationFile file)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyName);
        ArgumentNullException.ThrowIfNull(file);

        return GetAnchorNames(assemblyName, file.Name, file.Culture.Name);
    }

    /// <inheritdoc />
    protected override char FolderSeparator
        => '.';

    /// <inheritdoc />
    protected override IEnumerable<string> EnumerateNames()
        => _files.Keys;

    /// <inheritdoc />
    protected override string GetDefaultRootDir(string assemblyName)
        => assemblyName;

    // The build puts a namespace at the front of every name, so a type outside the root namespace is spelled from the start.
    /// <inheritdoc />
    protected override string GetRootDirOutsideRootNamespace(string rootDir)
        => string.Empty;

    /// <inheritdoc />
    protected override LocaleFile? ReadFile(string name, string cultureName)
        => _files.GetValueOrDefault(name)?.File;
}
