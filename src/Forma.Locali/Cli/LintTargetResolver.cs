namespace Forma.Locali.Cli;

using System.IO.Enumeration;

/// <summary>Reads a path as a solution, a project, a directory or a single file.</summary>
public sealed class LintTargetResolver : ILintTargetResolver
{
    private static readonly string[] SourceExtensions = [".cs", ".razor"];

    /// <inheritdoc />
    public LintTarget Resolve(string path, IReadOnlyCollection<string>? excludes = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var skip = excludes ?? [];

        if (Directory.Exists(path))
        {
            return FromFiles(Walk(path), skip);
        }

        if (!File.Exists(path))
        {
            return LintTarget.Empty;
        }

        return Path.GetExtension(path).ToUpperInvariant() switch
        {
            ".SLN" or ".SLNX" => FromSolution(path, skip),
            ".CSPROJ" => FromProject(path, skip),
            _ => FromFiles([path], skip),
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
            .Where(static file => IsLocalizationFile(file) || IsSourceFile(file));

    private static bool IsLocalizationFile(string file)
        => Path.GetExtension(file).Equals(".json", StringComparison.OrdinalIgnoreCase);

    private static bool IsSourceFile(string file)
        => SourceExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);

    private static LintTarget FromFiles(IEnumerable<string> files, IReadOnlyCollection<string> excludes)
    {
        var localization = new List<string>();
        var sources = new List<string>();
        var projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var owned = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            if (IsExcluded(file, excludes))
            {
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

        return new LintTarget(
            [.. localization],
            [.. sources],
            [.. projects],
            owned.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.OrdinalIgnoreCase));
    }

    private static LintTarget FromProject(string project, IReadOnlyCollection<string> excludes)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(project))!;
        var target = FromFiles(Walk(folder), excludes);

        return target with { Projects = [project] };
    }

    private static LintTarget FromSolution(string solution, IReadOnlyCollection<string> excludes)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(solution))!;
        var target = FromFiles(Walk(folder), excludes);

        return target with { Projects = [solution] };
    }

    // A glob reads against the whole path with forward slashes, so one pattern serves either platform,
    // and '*' spans separators so a folder can be named without counting the folders above it.
    private static bool IsExcluded(string file, IReadOnlyCollection<string> excludes)
    {
        if (excludes.Count == 0)
        {
            return false;
        }

        var path = Path.GetFullPath(file).Replace(Path.DirectorySeparatorChar, '/');

        return excludes.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, path, ignoreCase: true));
    }
}
