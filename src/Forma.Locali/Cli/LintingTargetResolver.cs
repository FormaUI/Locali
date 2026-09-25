namespace Forma.Locali.Cli;

using Forma.Locali.Configuration;

/// <summary>Reads a path as a solution, a project, a directory or a single file.</summary>
/// <param name="lintingConfiguration">Leaves out whatever the run's configuration excludes, from its <c>.locali.json</c> and its command line alike.</param>
public sealed class LintingTargetResolver(ILintingConfiguration lintingConfiguration) : ILintingTargetResolver
{
    private static readonly string[] SourceExtensions = [".cs", ".razor"];

    private readonly ILintingConfiguration _lintingConfiguration = lintingConfiguration ?? throw new ArgumentNullException(nameof(lintingConfiguration));

    /// <inheritdoc />
    public LintingTarget Resolve(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (Directory.Exists(path))
        {
            return FromFiles(Walk(path));
        }

        if (!File.Exists(path))
        {
            return LintingTarget.Empty;
        }

        return Path.GetExtension(path).ToUpperInvariant() switch
        {
            ".SLN" or ".SLNX" => FromSolution(path),
            ".CSPROJ" => FromProject(path),
            _ => FromFiles([path]),
        };
    }

    /// <summary>The project a source file belongs to, or null when no project sits above it.</summary>
    /// <param name="file">The source file to place.</param>
    public static string? ProjectOf(string file)
    {
        ArgumentException.ThrowIfNullOrEmpty(file);

        for (var folder = Directory.GetParent(Path.GetFullPath(file)); folder is not null; folder = folder.Parent)
        {
            var project = Directory.EnumerateFiles(folder.FullName, "*.csproj").FirstOrDefault();
            if (project is not null)
            {
                return project;
            }
        }

        return null;
    }

    // The build copies both translations and generated code beside the binaries, where reading them would double every report.
    private static bool IsBuildOutput(string file)
        => file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(static segment => segment is "bin" or "obj");

    private static IEnumerable<string> Walk(string folder)
        => Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(static file => !IsBuildOutput(file))
            .Where(static file => IsLocalizationFile(file) || IsSourceFile(file) || IsConfigurationFile(file));

    // The linting configuration file ends in .json like a locale file, but it is never one.
    private static bool IsLocalizationFile(string file)
        => Path.GetExtension(file).Equals(".json", StringComparison.OrdinalIgnoreCase)
            && !Path.GetFileName(file).Equals(LintingConfiguration.FileName, StringComparison.OrdinalIgnoreCase);

    private static bool IsSourceFile(string file)
        => SourceExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);

    private static bool IsConfigurationFile(string file)
        => Path.GetFileName(file).Equals(LintingConfiguration.FileName, StringComparison.OrdinalIgnoreCase);

    private LintingTarget FromFiles(IEnumerable<string> files)
    {
        var localization = new List<string>();
        var sources = new List<string>();
        var projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var owned = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var configurations = new List<string>();

        foreach (var file in files)
        {
            if (_lintingConfiguration.IsExcluded(file))
            {
                continue;
            }

            // A configuration file is listed so the run can say which it does not read; one a fixture folder keeps is excluded like the fixtures around it.
            if (IsConfigurationFile(file))
            {
                configurations.Add(file);
                continue;
            }

            if (IsLocalizationFile(file))
            {
                localization.Add(file);

                // The nearest project above a file owns it, so a nested project keeps its own translations.
                if (ProjectOf(file) is { } project)
                {
                    projects.Add(project);

                    if (!owned.TryGetValue(Path.GetFullPath(project), out var mine))
                    {
                        owned[Path.GetFullPath(project)] = mine = [];
                    }

                    mine.Add(file);
                }
            }
            else if (IsSourceFile(file))
            {
                sources.Add(file);

                if (ProjectOf(file) is { } project)
                {
                    projects.Add(project);
                }
            }
        }

        return new LintingTarget(
            [.. localization],
            [.. sources],
            [.. projects],
            owned.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.OrdinalIgnoreCase),
            [.. configurations]);
    }

    private LintingTarget FromProject(string project)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(project))!;
        var target = FromFiles(Walk(folder));

        return target with { Projects = [project] };
    }

    private LintingTarget FromSolution(string solution)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(solution))!;
        var target = FromFiles(Walk(folder));

        return target with { Projects = [solution], IsSolution = true };
    }
}
