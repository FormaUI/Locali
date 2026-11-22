namespace Forma.Locali.Cli;

/// <summary>The files a run was asked to read, split by how each is checked.</summary>
/// <param name="LocalizationFiles">The JSON files to check against the schema.</param>
/// <param name="SourceFiles">The C# and Razor files whose callsites are checked.</param>
/// <param name="Projects">The projects the source files belong to.</param>
/// <param name="LocalizationFilesByProject">The locale files each project owns, keyed by its full path.</param>
/// <param name="ConfigurationFiles">Every <c>.locali.json</c> the walk found, whether or not the run reads it.</param>
/// <param name="ProjectsByFile">The full path of the project owning each file the walk found, the nearest above it, keyed by the file's full path; a file no project sits above is absent.</param>
/// <param name="IsSolution">Whether the path named a solution, which lists every project there is.</param>
/// <param name="IsBoundToProjects">Whether the path named a solution or a project, so the run reads the files of the projects it loads and no others.</param>
public sealed record LintingTarget(
    IReadOnlyList<string> LocalizationFiles,
    IReadOnlyList<string> SourceFiles,
    IReadOnlyList<string> Projects,
    IReadOnlyDictionary<string, IReadOnlyList<string>> LocalizationFilesByProject,
    IReadOnlyList<string> ConfigurationFiles,
    IReadOnlyDictionary<string, string> ProjectsByFile,
    bool IsSolution = false,
    bool IsBoundToProjects = false)
{
    /// <summary>A target that names nothing.</summary>
    public static LintingTarget Empty { get; } = new([], [], [], new Dictionary<string, IReadOnlyList<string>>(), [], new Dictionary<string, string>());

    /// <summary>The locale files the project owns, empty when it owns none.</summary>
    /// <param name="projectPath">The project to read, as the loader reports its path.</param>
    public IReadOnlyList<string> LocalizationFilesOf(string projectPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);

        return LocalizationFilesByProject.TryGetValue(Path.GetFullPath(projectPath), out var files) ? files : [];
    }

    /// <summary>The target narrowed to the projects a run loaded: the files they own or build, and each configuration file among them or in a folder above one.</summary>
    /// <param name="projectPaths">The full path of each project the run loaded.</param>
    /// <param name="builtFiles">The full path of each file those projects compile or embed, wherever it sits.</param>
    public LintingTarget Within(IReadOnlySet<string> projectPaths, IReadOnlySet<string> builtFiles)
    {
        ArgumentNullException.ThrowIfNull(projectPaths);
        ArgumentNullException.ThrowIfNull(builtFiles);

        bool IsOfRun(string file)
        {
            var fullPath = Path.GetFullPath(file);

            return builtFiles.Contains(fullPath) || (ProjectsByFile.TryGetValue(fullPath, out var project) && projectPaths.Contains(project));
        }

        // A configuration file no project of the run owns still governs every project of the run beneath its folder.
        bool IsAboveRun(string file)
        {
            var folder = Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(Path.GetFullPath(file))!) + Path.DirectorySeparatorChar;

            return projectPaths.Any(project => project.StartsWith(folder, StringComparison.OrdinalIgnoreCase));
        }

        return this with
        {
            LocalizationFiles = [.. LocalizationFiles.Where(IsOfRun)],
            SourceFiles = [.. SourceFiles.Where(IsOfRun)],
            LocalizationFilesByProject = LocalizationFilesByProject
                .Where(pair => projectPaths.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            ConfigurationFiles = [.. ConfigurationFiles.Where(file => IsOfRun(file) || IsAboveRun(file))],
        };
    }

    /// <summary>How many files the target names in total.</summary>
    public int FileCount
        => LocalizationFiles.Count + SourceFiles.Count;
}
