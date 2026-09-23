namespace Forma.Locali.Configuration;

using System.Collections.Concurrent;

using Forma.Locali.Cli;
using Forma.Locali.Diagnostics;

/// <summary>Resolves the <c>.locali.json</c> files that govern one run, found the way an <c>.editorconfig</c> is: by walking up from each file the run reads, merging every ancestor's entries into the file's own.</summary>
public sealed class LintingConfiguration : ILintingConfiguration
{
    /// <summary>The name a linting configuration file is read under.</summary>
    public const string FileName = ".locali.json";

    private readonly ConcurrentDictionary<string, IReadOnlyList<TrackedFile>> _ancestryByDirectory = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TrackedFile> _filesByPath = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public bool IsExcluded(string file)
    {
        ArgumentException.ThrowIfNullOrEmpty(file);

        var matched = false;

        foreach (var (tracked, relative) in Ancestry(file))
        {
            if (tracked.File is not { } config)
            {
                continue;
            }

            foreach (var entry in config.ExcludeFiles)
            {
                if (!GlobPattern.Matches(entry.Glob, relative))
                {
                    continue;
                }

                tracked.UsedExclude.TryAdd(entry.Offset, true);
                matched = true;
            }
        }

        return matched;
    }

    /// <inheritdoc />
    public IReadOnlySet<LintingCode> GetIgnoredCodes(string file)
    {
        ArgumentException.ThrowIfNullOrEmpty(file);

        var ignored = new HashSet<LintingCode>();

        foreach (var (tracked, relative) in Ancestry(file))
        {
            if (tracked.File is not { } config)
            {
                continue;
            }

            foreach (var entry in config.IgnoreDiagnostics)
            {
                if (!GlobPattern.Matches(entry.Glob, relative))
                {
                    continue;
                }

                tracked.UsedIgnore.TryAdd(entry.Offset, true);

                foreach (var diagnostic in entry.Entries)
                {
                    // A rule judging the linting configuration cannot be excused by that same configuration.
                    if (Resolve(diagnostic.Id) is { } known && known != LintingCodes.MalformedConfiguration && known != LintingCodes.StaleConfigurationEntry)
                    {
                        ignored.Add(known);
                    }
                }
            }
        }

        return ignored;
    }

    // An allow list cannot be widened or narrowed by union, so the nearest file naming one speaks for the tree beneath it.
    /// <inheritdoc />
    public LocaleSources? GetLocaleSources(string file)
    {
        ArgumentException.ThrowIfNullOrEmpty(file);

        foreach (var (tracked, _) in Ancestry(file))
        {
            if (tracked.File?.LocaleSources is { } localeSources)
            {
                tracked.UsedLocaleSources.TryAdd(localeSources.Offset, true);

                return localeSources.Allowed;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public IReadOnlyList<LintingDiagnostic> GetDiagnostics()
    {
        var diagnostics = new List<LintingDiagnostic>();

        foreach (var tracked in _filesByPath.Values)
        {
            DiagnoseFile(tracked, diagnostics);
        }

        return diagnostics;
    }

    private static void DiagnoseFile(TrackedFile tracked, List<LintingDiagnostic> diagnostics)
    {
        if (tracked.File is not { } config)
        {
            diagnostics.Add(new LintingDiagnostic(
                tracked.Path,
                1,
                1,
                LintingCodes.MalformedConfiguration.DefaultSeverity,
                LintingCodes.MalformedConfiguration,
                $"The file is not readable JSON: {tracked.ReadError}."));

            return;
        }

        foreach (var malformedValue in config.MalformedValues)
        {
            diagnostics.Add(config.Diagnose(malformedValue.Offset, LintingCodes.MalformedConfiguration, malformedValue.Reason));
        }

        foreach (var entry in config.IgnoreDiagnostics)
        {
            foreach (var diagnostic in entry.Entries)
            {
                if (Resolve(diagnostic.Id) is null)
                {
                    diagnostics.Add(config.Diagnose(
                        diagnostic.Offset,
                        LintingCodes.MalformedConfiguration,
                        $"'{diagnostic.Id}' is not a rule this linter defines, so it leaves nothing out."));
                }
            }

            if (!tracked.UsedIgnore.ContainsKey(entry.Offset))
            {
                diagnostics.Add(config.Diagnose(
                    entry.Offset,
                    LintingCodes.StaleConfigurationEntry,
                    $"'{entry.Glob}' matched no file the run linted, so the rules it lists apply nowhere."));
            }
        }

        foreach (var entry in config.ExcludeFiles)
        {
            if (!tracked.UsedExclude.ContainsKey(entry.Offset))
            {
                diagnostics.Add(config.Diagnose(
                    entry.Offset,
                    LintingCodes.StaleConfigurationEntry,
                    $"'{entry.Glob}' matched no file the run reached, so it excludes nothing."));
            }
        }

        // A restated list outlives the files it was written for, or is shadowed by every nearer file, and either way governs nothing.
        if (config.LocaleSources is { } localeSources && !tracked.UsedLocaleSources.ContainsKey(localeSources.Offset))
        {
            diagnostics.Add(config.Diagnose(
                localeSources.Offset,
                LintingCodes.StaleConfigurationEntry,
                "'localeSources' governs no locale file the run reached, so it allows and forbids nothing."));
        }
    }

    private static LintingCode? Resolve(string identifier)
        => LintingCodes.All.FirstOrDefault(code => code.Id == identifier);

    // Every ancestor's file, paired with the path relative to that ancestor's own directory, so a config
    // travels with the tree it sits over rather than assuming everyone's absolute layout matches.
    private IEnumerable<(TrackedFile Tracked, string Relative)> Ancestry(string file)
    {
        var full = Path.GetFullPath(file);
        var directory = Path.GetDirectoryName(full);

        if (directory is null)
        {
            yield break;
        }

        foreach (var tracked in _ancestryByDirectory.GetOrAdd(directory, BuildAncestry))
        {
            yield return (tracked, GlobPattern.Normalize(Path.GetRelativePath(Path.GetDirectoryName(tracked.Path)!, full)));
        }
    }

    private IReadOnlyList<TrackedFile> BuildAncestry(string startDirectory)
    {
        var found = new List<TrackedFile>();

        for (var folder = new DirectoryInfo(startDirectory); folder is not null; folder = folder.Parent)
        {
            var candidate = Path.Combine(folder.FullName, FileName);

            if (File.Exists(candidate))
            {
                found.Add(_filesByPath.GetOrAdd(candidate, Load));
            }
        }

        return found;
    }

    private static TrackedFile Load(string path)
    {
        byte[] content;
        try
        {
            content = File.ReadAllBytes(path);
        }
        catch (IOException exception)
        {
            return new TrackedFile(path, null, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return new TrackedFile(path, null, exception.Message);
        }

        return LocaliConfigFile.TryRead(path, content, out var file, out var error)
            ? new TrackedFile(path, file, null)
            : new TrackedFile(path, null, error);
    }

    // One entry per .locali.json found, shared across every directory whose ancestry reaches it, so a glob's
    // usage is counted once for the whole run rather than once per directory that happens to walk past it.
    private sealed class TrackedFile(string path, LocaliConfigFile? file, string? readError)
    {
        public string Path { get; } = path;

        public LocaliConfigFile? File { get; } = file;

        public string? ReadError { get; } = readError;

        public ConcurrentDictionary<int, bool> UsedIgnore { get; } = new();

        public ConcurrentDictionary<int, bool> UsedExclude { get; } = new();

        public ConcurrentDictionary<int, bool> UsedLocaleSources { get; } = new();
    }
}
