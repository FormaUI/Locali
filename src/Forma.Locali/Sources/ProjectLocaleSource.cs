namespace Forma.Locali.Sources;

using Forma.Framework.Localization.Files;

/// <summary>A project's translation files, read from its source tree under the names the build embeds them by, so the runtime's own walk finds them as it would in the built assembly.</summary>
public sealed class ProjectLocaleSource : LocaleSource
{
    private readonly Dictionary<string, TranslationFile> _files = new(StringComparer.Ordinal);

    /// <summary>Indexes the files by every name each could be asked for.</summary>
    /// <param name="files">The project's translation files.</param>
    /// <param name="format">The format the files are written in.</param>
    public ProjectLocaleSource(IReadOnlyList<TranslationFile> files, ILocaleFileFormat format)
        : base(format)
    {
        ArgumentNullException.ThrowIfNull(files);

        // A name two files could answer to goes to the one shedding the fewest folders, whatever order the files came in.
        var ranks = files.Count == 0 ? 0 : files.Max(file => file.Names.Count);
        for (var rank = 0; rank < ranks; rank++)
        {
            foreach (var file in files)
            {
                if (rank < file.Names.Count)
                {
                    _files.TryAdd(file.Names[rank], file);
                }
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

    /// <inheritdoc />
    protected override bool ReadsDeclaredRoots => false;

    /// <inheritdoc />
    protected override IReadOnlyList<string> GetLeadingSegments(string assemblyName, bool rootedAtAssembly)
        => rootedAtAssembly ? [assemblyName] : [];

    /// <inheritdoc />
    protected override string GetName(IReadOnlyList<string> segments, string cultureName)
        => $"{string.Join('.', segments)}.{cultureName}{Format.Extension}";

    /// <inheritdoc />
    protected override Stream? Open(IReadOnlyList<string> segments, string cultureName)
        => _files.TryGetValue(GetName(segments, cultureName), out var file) ? File.OpenRead(file.Path) : null;
}
