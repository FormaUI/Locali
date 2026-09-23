namespace Forma.Locali.Sources;

using Forma.Framework.Localization.Files;

/// <summary>A locale pack's files, read from its source tree under the names its build embeds them by and laid out as the runtime lays a pack out: a folder per assembly, by the root namespace the assembly's own config declares, with no config of the pack's own.</summary>
public sealed class PackLocaleSource : LocaleSource
{
    private readonly string _packName;
    private readonly Func<string, string?> _rootNamespaces;
    private readonly Dictionary<string, ProjectLocaleFile> _files = [with(StringComparer.Ordinal)];
    private readonly Dictionary<string, ProjectLocaleFile> _filesByDisplayName = [with(StringComparer.Ordinal)];

    /// <summary>Indexes the files by the name the build embeds each under.</summary>
    /// <param name="packName">The name of the assembly the pack builds, which every file is reported under.</param>
    /// <param name="files">Every locale file of the pack, whatever assembly its folder names.</param>
    /// <param name="rootNamespaces">The root namespace each assembly's own config declares, by the assembly's name, or null where it declares none.</param>
    /// <param name="format">The format the files are written in.</param>
    public PackLocaleSource(string packName, IReadOnlyList<ProjectLocaleFile> files, Func<string, string?> rootNamespaces, ILocaleFileFormat format)
        : base(format)
    {
        ArgumentException.ThrowIfNullOrEmpty(packName);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(rootNamespaces);

        _packName = packName;
        _rootNamespaces = rootNamespaces;

        foreach (var file in files)
        {
            _files[file.Name] = file;
            _filesByDisplayName[GetDisplayName(file.Name)] = file;
        }
    }

    /// <summary>The file the source serves under a name, or null when none answers to it.</summary>
    /// <param name="name">The name, as the source's entries report it.</param>
    public ProjectLocaleFile? GetFile(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return _filesByDisplayName.GetValueOrDefault(name);
    }

    /// <summary>The full name of every type or namespace the runtime reads a file for in an assembly, none when no path in force reads it.</summary>
    /// <param name="assemblyName">The name of the assembly the file's folder names.</param>
    /// <param name="file">The file.</param>
    public IReadOnlyList<string> GetAnchorNames(string assemblyName, ProjectLocaleFile file)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyName);
        ArgumentNullException.ThrowIfNull(file);

        return GetAnchorNames(assemblyName, file.Name, file.Culture.Name);
    }

    /// <inheritdoc />
    protected override char FolderSeparator
        => '.';

    // A pack is read as a host's folder is, so a file sits in a folder's own locales folder or directly in the folder.
    /// <inheritdoc />
    protected override IReadOnlyList<LocalePath> DefaultPaths
        => LocalePath.HostDefaults;

    /// <inheritdoc />
    protected override IEnumerable<string> EnumerateNames()
        => _files.Keys;

    /// <inheritdoc />
    protected override string GetDefaultRootDir(string assemblyName)
        => assemblyName;

    // A pack carries no config of its own; the one fact its layout follows is the root namespace the assembly declares.
    /// <inheritdoc />
    protected override (string Folder, LocaleConfig Config)? GetConfig(string assemblyName)
        => (assemblyName, new LocaleConfig(_rootNamespaces(assemblyName), null, null, null));

    // Several packs can carry a file for one anchor, so what is reported says which pack to open.
    /// <inheritdoc />
    protected override string GetDisplayName(string name)
        => $"{_packName}/{name}";

    /// <inheritdoc />
    protected override LocaleFile? ReadFile(string name, string cultureName)
        => _files.GetValueOrDefault(name)?.File;
}
