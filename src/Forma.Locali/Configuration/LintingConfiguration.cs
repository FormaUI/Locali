namespace Forma.Locali.Configuration;

using System.Collections.Concurrent;

using Forma.Locali.Cli;
using Forma.Locali.Diagnostics;

/// <summary>Reads the one <c>.locali.json</c> that governs a run, the nearest at or above the path the run was given, and holds every entry in it to having applied to something.</summary>
public sealed class LintingConfiguration : ILintingConfiguration
{
    /// <summary>The name a linting configuration file is read under.</summary>
    public const string FileName = ".locali.json";

    private readonly Lazy<TrackedFile?> _file;

    /// <summary>Finds the configuration for a run.</summary>
    /// <param name="path">The path the run was given, a file or a directory.</param>
    public LintingConfiguration(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        _file = new Lazy<TrackedFile?>(() => Find(path));
    }

    /// <inheritdoc />
    public string? FilePath
        => _file.Value?.Path;

    /// <inheritdoc />
    public bool IsExcluded(string file)
    {
        ArgumentException.ThrowIfNullOrEmpty(file);

        if (_file.Value is not { File: { } config } tracked)
        {
            return false;
        }

        var relative = Relative(tracked, file);
        var matched = false;

        foreach (var entry in config.ExcludeFiles)
        {
            if (!GlobPattern.Matches(entry.Glob, relative))
            {
                continue;
            }

            tracked.UsedExclude.TryAdd(entry.Offset, true);
            matched = true;
        }

        return matched;
    }

    /// <inheritdoc />
    public IReadOnlySet<LintingCode> GetIgnoredCodes(string file)
    {
        ArgumentException.ThrowIfNullOrEmpty(file);

        var ignored = new HashSet<LintingCode>();

        if (_file.Value is not { File: { } config } tracked)
        {
            return ignored;
        }

        var relative = Relative(tracked, file);

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

        return ignored;
    }

    // An allow list cannot be merged by union, so the last line matching a file speaks for it, and a line is written general first and specific after.
    /// <inheritdoc />
    public LocaleSources? GetLocaleSources(string file)
    {
        ArgumentException.ThrowIfNullOrEmpty(file);

        if (_file.Value is not { File: { } config } tracked)
        {
            return null;
        }

        var relative = Relative(tracked, file);
        ConfigLocaleSource? last = null;

        foreach (var entry in config.LocaleSources)
        {
            if (GlobPattern.Matches(entry.Glob, relative))
            {
                last = entry;
            }
        }

        if (last is not { } winner)
        {
            return null;
        }

        tracked.UsedLocaleSources.TryAdd(winner.Offset, true);

        return winner.Allowed;
    }

    /// <inheritdoc />
    public IReadOnlyList<LintingDiagnostic> GetDiagnostics()
    {
        var diagnostics = new List<LintingDiagnostic>();

        if (_file.Value is { } tracked)
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

        // A line matching no locale file, or one every later line overrides for each file it matches, governs nothing.
        foreach (var entry in config.LocaleSources)
        {
            if (!tracked.UsedLocaleSources.ContainsKey(entry.Offset))
            {
                diagnostics.Add(config.Diagnose(
                    entry.Offset,
                    LintingCodes.StaleConfigurationEntry,
                    $"'{entry.Glob}' is the last line matching no locale file the run reached, so it allows and forbids nothing."));
            }
        }
    }

    private static LintingCode? Resolve(string identifier)
        => LintingCodes.All.FirstOrDefault(code => code.Id == identifier);

    // A file's path relative to the configuration's own directory, so a glob reads as written from where the file sits.
    private static string Relative(TrackedFile tracked, string file)
        => GlobPattern.Normalize(Path.GetRelativePath(Path.GetDirectoryName(tracked.Path)!, Path.GetFullPath(file)));

    // The nearest file wins, and a run given a folder above every configuration reads none, since a run is one repository's.
    private static TrackedFile? Find(string path)
    {
        var full = Path.GetFullPath(path);

        for (var folder = new DirectoryInfo(Directory.Exists(full) ? full : Path.GetDirectoryName(full)!); folder is not null; folder = folder.Parent)
        {
            var candidate = Path.Combine(folder.FullName, FileName);

            if (File.Exists(candidate))
            {
                return Load(candidate);
            }
        }

        return null;
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

    // The one file the run reads, with each entry's usage counted across every file the run asks about, from whichever thread asks.
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
