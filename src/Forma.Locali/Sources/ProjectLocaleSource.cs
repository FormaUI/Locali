namespace Forma.Locali.Sources;

using Forma.Framework.Localization.Files;

/// <summary>A project's translation files, read once from its source tree and served under the names the build embeds them by, so the runtime's own walk finds them as it would in the built assembly.</summary>
public sealed class ProjectLocaleSource : LocaleSource
{
    private readonly Dictionary<string, TranslationFile> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _contested = new(StringComparer.Ordinal);

    /// <summary>Indexes the files by every name each could be asked for, leaving out a file whose name another file claims too.</summary>
    /// <param name="files">The project's translation files.</param>
    /// <param name="format">The format the files are written in.</param>
    public ProjectLocaleSource(IReadOnlyList<TranslationFile> files, ILocaleFileFormat format)
        : base(format)
    {
        ArgumentNullException.ThrowIfNull(files);

        // A name two files could answer to depends on a ResourcesPath the project does not record, so neither is served.
        var claims = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var name in files.SelectMany(file => file.Names))
        {
            claims[name] = claims.GetValueOrDefault(name) + 1;
        }

        _contested.UnionWith(claims.Where(claim => claim.Value > 1).Select(claim => claim.Key));

        foreach (var file in files.Where(file => !IsContested(file)))
        {
            foreach (var name in file.Names)
            {
                _files[name] = file;
            }
        }
    }

    /// <summary>The file a name the walk asked for reads, or null when none answers to it.</summary>
    /// <param name="name">The name, as the walk's entries report it.</param>
    public TranslationFile? GetFile(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return _files.GetValueOrDefault(name);
    }

    /// <summary>Whether another file claims one of the file's names, so which of them the runtime reads cannot be told.</summary>
    /// <param name="file">The file.</param>
    public bool IsContested(TranslationFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return file.Names.Any(_contested.Contains);
    }

    /// <inheritdoc />
    protected override bool ReadsDeclaredRoots => false;

    /// <inheritdoc />
    protected override IReadOnlyList<string> GetLeadingSegments(string assemblyName, bool rootedAtAssembly)
        => rootedAtAssembly ? [assemblyName] : [];

    /// <inheritdoc />
    protected override string GetFileName(IReadOnlyList<string> segments, string cultureName)
        => $"{string.Join('.', segments)}.{cultureName}{Format.Extension}";

    /// <inheritdoc />
    protected override LocaleFile? ReadFile(IReadOnlyList<string> segments, string cultureName)
        => _files.GetValueOrDefault(GetFileName(segments, cultureName))?.File;
}
