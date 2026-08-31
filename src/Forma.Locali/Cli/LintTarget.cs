namespace Forma.Locali.Cli;

/// <summary>The files a run was asked to read, split by how each is checked.</summary>
/// <param name="LocalizationFiles">The JSON files to check against the schema.</param>
/// <param name="SourceFiles">The C# and Razor files whose call sites are checked.</param>
/// <param name="Projects">The projects the source files belong to.</param>
/// <param name="LocalizationFilesByProject">The translation files each project owns, keyed by its full path.</param>
public sealed record LintTarget(
    IReadOnlyList<string> LocalizationFiles,
    IReadOnlyList<string> SourceFiles,
    IReadOnlyList<string> Projects,
    IReadOnlyDictionary<string, IReadOnlyList<string>> LocalizationFilesByProject)
{
    /// <summary>A target that names nothing.</summary>
    public static LintTarget Empty { get; } = new([], [], [], new Dictionary<string, IReadOnlyList<string>>());

    /// <summary>The translation files the project owns, empty when it owns none.</summary>
    /// <param name="projectPath">The project to read, as the loader reports its path.</param>
    public IReadOnlyList<string> LocalizationFilesOf(string projectPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);

        return LocalizationFilesByProject.TryGetValue(Path.GetFullPath(projectPath), out var files) ? files : [];
    }

    /// <summary>How many files the target names in total.</summary>
    public int FileCount
        => LocalizationFiles.Count + SourceFiles.Count;
}
