namespace Forma.Locali.Sources;

using Forma.Framework.Localization.Files;

/// <summary>A project's translation and settings files, read from its source tree and served under the names the build embeds them by, so the runtime's own walk finds them as it would in the built assembly.</summary>
public sealed class ProjectLocaleSource : LocaleSource
{
    private readonly Dictionary<string, TranslationFile> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _settingsPaths = new(StringComparer.Ordinal);

    /// <summary>Indexes the files by the name the build embeds each under.</summary>
    /// <param name="files">The project's translation files.</param>
    /// <param name="settingsPaths">The full path of each settings file the project embeds, keyed by the name the build embeds it under.</param>
    /// <param name="format">The format the files are written in.</param>
    public ProjectLocaleSource(IReadOnlyList<TranslationFile> files, IReadOnlyDictionary<string, string> settingsPaths, ILocaleFileFormat format)
        : base(format)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(settingsPaths);

        foreach (var file in files)
        {
            _files[file.Name] = file;
        }

        foreach (var (name, path) in settingsPaths)
        {
            _settingsPaths[name] = path;
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
        => _files.Keys.Concat(_settingsPaths.Keys);

    /// <inheritdoc />
    protected override string GetDefaultRootDir(string assemblyName)
        => assemblyName;

    // The build puts a namespace at the front of every name, so a type outside the root namespace is spelled from the start.
    /// <inheritdoc />
    protected override string GetRootDirOutsideRootNamespace(string rootDir)
        => string.Empty;

    // No embedded name reaches above the project's root namespace, so a root climbing past it lands there.
    /// <inheritdoc />
    protected override string GetRootDirPastStart(string settingsName, string rootDir, string rootNamespace)
        => rootNamespace;

    // A settings file is named by its path in anything the walk throws; a locale file keeps the name the walk's entries report it by.
    /// <inheritdoc />
    protected override string GetDisplayName(string name)
        => _settingsPaths.GetValueOrDefault(name) ?? name;

    /// <inheritdoc />
    protected override LocaleFile? ReadFile(string name, string cultureName)
        => _files.GetValueOrDefault(name)?.File;

    // What is wrong with a settings file is its own diagnostics' business, and the walk goes on as if it said nothing.
    /// <inheritdoc />
    protected override LocaliSettings ReadSettings(string name)
    {
        try
        {
            return base.ReadSettings(name);
        }
        catch (LocaliSettingsException)
        {
            return LocaliSettings.Empty;
        }
    }

    /// <inheritdoc />
    protected override Stream? OpenFile(string name)
    {
        if (!_settingsPaths.TryGetValue(name, out var path))
        {
            return null;
        }

        try
        {
            return File.OpenRead(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
