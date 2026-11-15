namespace Forma.Locali.Sources;

using Forma.Framework.Localization.Files;

/// <summary>Locale files read by names that each start with the name of the assembly whose strings they hold, as the runtime reads a locale pack and an application's files for other assemblies: each name going on from the root namespace that assembly's own config declares, without the source's folder of locale files.</summary>
public sealed class AssemblyNamedLocaleSource : LocaleSource
{
    private readonly string _displayPrefix;
    private readonly Func<string, string?> _rootNamespaces;
    private readonly string _localesDir;
    private readonly Dictionary<string, ProjectLocaleFile> _files = [with(StringComparer.Ordinal)];
    private readonly Dictionary<string, ProjectLocaleFile> _filesByDisplayName = [with(StringComparer.Ordinal)];
    private readonly Dictionary<string, string> _namesByPath = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Indexes the files by the names the source reads them by.</summary>
    /// <param name="displayPrefix">What every file's name is reported after: a pack's name and a folder separator, or where an application's names begin.</param>
    /// <param name="files">Every file of the source with the name the source reads it by, whatever assembly that name starts with; of two files of one name, the later is read.</param>
    /// <param name="rootNamespaces">The root namespace each assembly's own config declares, by the assembly's name, or null where it declares none.</param>
    /// <param name="localesDir">The folder of locale files the names are read without: an application's own, or <c>.locales</c> for a pack.</param>
    /// <param name="format">The format the files are written in.</param>
    public AssemblyNamedLocaleSource(string displayPrefix, IEnumerable<(string Name, ProjectLocaleFile File)> files, Func<string, string?> rootNamespaces, string localesDir, ILocaleFileFormat format)
        : base(format)
    {
        ArgumentNullException.ThrowIfNull(displayPrefix);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(rootNamespaces);
        ArgumentException.ThrowIfNullOrEmpty(localesDir);

        _displayPrefix = displayPrefix;
        _rootNamespaces = rootNamespaces;
        _localesDir = localesDir;

        foreach (var (name, file) in files)
        {
            _files[name] = file;
            _filesByDisplayName[GetDisplayName(name)] = file;
            _namesByPath[file.Path] = name;
        }
    }

    /// <summary>The file the source serves under a name, or null when none answers to it.</summary>
    /// <param name="name">The name, as the source's entries report it.</param>
    public ProjectLocaleFile? GetFile(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return _filesByDisplayName.GetValueOrDefault(name);
    }

    /// <summary>The name the source reads a file by, or null when the file is none of the source's.</summary>
    /// <param name="file">The file.</param>
    public string? GetName(ProjectLocaleFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return _namesByPath.GetValueOrDefault(file.Path);
    }

    /// <inheritdoc />
    protected override char FolderSeparator
        => '.';

    /// <inheritdoc />
    protected override bool NamesStartWithAssemblyName
        => true;

    /// <inheritdoc />
    protected override IEnumerable<string> EnumerateNames()
        => _files.Keys;

    /// <inheritdoc />
    protected override string GetDefaultRootDir(string assemblyName)
        => assemblyName;

    // The source carries no config of its own; its names follow the root namespace the assembly declares, without the source's folder of locale files.
    /// <inheritdoc />
    protected override (string Folder, LocaleConfig Config)? GetConfig(string assemblyName)
        => (assemblyName, new LocaleConfig(_rootNamespaces(assemblyName), null, _localesDir, null));

    // Several sources can carry a file for one name, so what is reported says which to open.
    /// <inheritdoc />
    protected override string GetDisplayName(string name)
        => _displayPrefix + name;

    /// <inheritdoc />
    protected override LocaleFile? ReadFile(string name, string cultureName)
        => _files.GetValueOrDefault(name)?.File;
}
