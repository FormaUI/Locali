namespace Forma.Locali.Configuration;

using System.Collections.Concurrent;

using Forma.Locali.Cli;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>The configuration a run applies: the one <c>.locali.json</c> nearest at or above the path the run was given, merged with the globs the command line excludes, holding each of the file's exceptions and source lines that can match only what the run reads to having applied to something.</summary>
public sealed class LintingConfiguration : ILintingConfiguration
{
    /// <summary>The name a linting configuration file is read under.</summary>
    public const string FileName = ".locali.json";

    private static readonly char[] Wildcards = ['*', '?', '\\'];

    private readonly Lazy<TrackedFile?> _file;
    private readonly IReadOnlyCollection<string> _commandLineExcludes;
    private readonly string _scope;

    /// <summary>Finds the configuration for a run and merges the command line into it.</summary>
    /// <param name="path">The path the run was given, a file or a directory, whose reach bounds the entries held to having applied to something.</param>
    /// <param name="excludes">The globs the command line excludes, or null when it excludes none.</param>
    public LintingConfiguration(string path, IReadOnlyCollection<string>? excludes = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        _file = new Lazy<TrackedFile?>(() => Find(path));
        _commandLineExcludes = excludes ?? [];
        _scope = LintingTargetResolver.ScopeOf(path);
    }

    /// <inheritdoc />
    public string? FilePath
        => _file.Value?.Path;

    /// <inheritdoc />
    public bool IsExcluded(string file)
    {
        ArgumentException.ThrowIfNullOrEmpty(file);

        var full = Path.GetFullPath(file);

        if (_file.Value is { File: { } config } tracked && config.ExcludeFiles.Any(glob => GlobPattern.Matches(glob, Relative(tracked, full))))
        {
            return true;
        }

        return _commandLineExcludes.Any(pattern => GlobPattern.Matches(pattern, full));
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
            DiagnoseFile(tracked, ScopeIn(tracked), diagnostics);
        }

        return diagnostics;
    }

    /// <inheritdoc />
    public LocaleHints ReadHints(IReadOnlyCollection<HintsFolder> libraries)
    {
        ArgumentNullException.ThrowIfNull(libraries);

        var hints = new List<LocaleMemberHint>();
        var keys = new List<LocaleKeyHint>();
        var diagnostics = new List<LintingDiagnostic>();

        // A library's hints describe its own code and travel with it: a run building the library reads them from its project, and a
        // run restoring it from its package.
        foreach (var library in libraries)
        {
            var path = Path.Combine(library.Path, LocaleHintsFile.FileName);
            if (File.Exists(path))
            {
                Import(path, library, hints, keys, diagnostics);
            }
        }

        // A hint is held only to a run reading the configuration's whole folder, beneath which the project building its assembly sits.
        if (_file.Value is { File: { } config } tracked)
        {
            var isHeldToRun = ScopeIn(tracked) == ".";
            hints.AddRange(config.LocaleMembers.Select(member => Hint(member, config.Path, config.Map, isPackaged: false) with { IsHeldToRun = isHeldToRun }));
            keys.AddRange(config.LocaleKeys.Select(key => Hint(key, config.Path, config.Map, isPackaged: false) with { IsHeldToRun = isHeldToRun }));
        }

        return new LocaleHints(hints, keys, diagnostics);
    }

    // What is wrong in a package's file is the package's to fix, so the run restoring it hears of it as a warning.
    private static void Import(string path, HintsFolder library, List<LocaleMemberHint> hints, List<LocaleKeyHint> keys, List<LintingDiagnostic> diagnostics)
    {
        var severity = library.IsPackage ? LintingSeverity.Warning : LintingCodes.MalformedConfiguration.DefaultSeverity;
        byte[] content;
        try
        {
            content = File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new LintingDiagnostic(path, 1, 1, severity, LintingCodes.MalformedConfiguration, $"The hints file could not be read: {exception.Message}"));

            return;
        }

        if (!LocaleHintsFile.TryRead(path, content, out var file, out var error) || file is null)
        {
            diagnostics.Add(new LintingDiagnostic(path, 1, 1, severity, LintingCodes.MalformedConfiguration, $"The hints file is not readable JSON: {error}."));

            return;
        }

        diagnostics.AddRange(file.MalformedValues.Select(malformed => file.Diagnose(malformed.Offset, LintingCodes.MalformedConfiguration, malformed.Reason) with { Severity = severity }));
        foreach (var member in file.LocaleMembers)
        {
            var hint = Hint(member, file.Path, file.Map, library.IsPackage);
            if (library.Assemblies.Contains(hint.Assembly, StringComparer.Ordinal))
            {
                hints.Add(hint);
                continue;
            }

            diagnostics.Add(hint.Diagnose(LintingCodes.InvalidHint, ForeignAssembly(hint.Assembly)));
        }

        foreach (var key in file.LocaleKeys)
        {
            var hint = Hint(key, file.Path, file.Map, library.IsPackage);
            if (library.Assemblies.Contains(hint.Assembly, StringComparer.Ordinal))
            {
                keys.Add(hint);
                continue;
            }

            diagnostics.Add(hint.Diagnose(LintingCodes.InvalidHint, ForeignAssembly(hint.Assembly)));
        }
    }

    private static string ForeignAssembly(string assembly)
        => $"'{assembly}' is not an assembly the library this file belongs to builds or ships, so the hint relates nothing; a library's hints speak for its own code, and an application states the rest in its .locali.json.";

    private static LocaleMemberHint Hint(ConfigLocaleMember member, string path, TextMap map, bool isPackaged)
    {
        var (line, column) = map.Locate(member.Offset);

        return new LocaleMemberHint(member.Assembly, member.Type, member.Member, member.TypeMember, path, line, column) { IsPackaged = isPackaged };
    }

    private static LocaleKeyHint Hint(ConfigLocaleKey key, string path, TextMap map, bool isPackaged)
    {
        var (line, column) = map.Locate(key.Offset);

        return new LocaleKeyHint(key.Assembly, key.Type, key.Method, key.Parameter, key.Locale, key.TypeParameter, path, line, column) { IsPackaged = isPackaged };
    }

    private static void DiagnoseFile(TrackedFile tracked, string scope, List<LintingDiagnostic> diagnostics)
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
                // A retired rule reports nowhere, so a configuration still naming it leaves nothing out and fails no run.
                if (LintingCodes.Retired.Contains(diagnostic.Id))
                {
                    diagnostics.Add(config.Diagnose(
                        diagnostic.Offset,
                        LintingCodes.StaleConfigurationEntry,
                        $"'{diagnostic.Id}' is a rule this linter has retired, so it leaves nothing out."));
                }
                else if (Resolve(diagnostic.Id) is null)
                {
                    diagnostics.Add(config.Diagnose(
                        diagnostic.Offset,
                        LintingCodes.MalformedConfiguration,
                        $"'{diagnostic.Id}' is not a rule this linter defines, so it leaves nothing out."));
                }
            }

            if (!tracked.UsedIgnore.ContainsKey(entry.Offset) && IsWithin(entry.Glob, scope))
            {
                diagnostics.Add(config.Diagnose(
                    entry.Offset,
                    LintingCodes.StaleConfigurationEntry,
                    $"'{entry.Glob}' matched no file the run linted, so the rules it lists apply nowhere."));
            }
        }

        // A line matching no locale file, or one every later line overrides for each file it matches, governs nothing.
        foreach (var entry in config.LocaleSources)
        {
            if (!tracked.UsedLocaleSources.ContainsKey(entry.Offset) && IsWithin(entry.Glob, scope))
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

    // A glob matches only paths starting with the text before its first wildcard, so it is judged only where every such path lies in what the run reads.
    private static bool IsWithin(string glob, string scope)
    {
        if (scope == ".")
        {
            return true;
        }

        var wildcard = glob.IndexOfAny(Wildcards);
        var literal = wildcard < 0 ? glob : glob[..wildcard];

        return literal.StartsWith(scope + "/", StringComparison.OrdinalIgnoreCase)
            || (wildcard < 0 && literal.Equals(scope, StringComparison.OrdinalIgnoreCase));
    }

    // A file's path relative to the configuration's own directory, so a glob reads as written from where the file sits.
    private static string Relative(TrackedFile tracked, string file)
        => GlobPattern.Normalize(Path.GetRelativePath(Path.GetDirectoryName(tracked.Path)!, Path.GetFullPath(file)));

    // What the run reads as a glob writes it, from the configuration's own directory, which is "." where the run reads all of it.
    private string ScopeIn(TrackedFile tracked)
        => GlobPattern.Normalize(Path.GetRelativePath(Path.GetDirectoryName(tracked.Path)!, _scope));

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

        return LinterConfigFile.TryRead(path, content, out var file, out var error)
            ? new TrackedFile(path, file, null)
            : new TrackedFile(path, null, error);
    }

    // The one file the run reads, with each entry's usage counted across every file the run asks about, from whichever thread asks.
    private sealed class TrackedFile(string path, LinterConfigFile? file, string? readError)
    {
        public string Path { get; } = path;

        public LinterConfigFile? File { get; } = file;

        public string? ReadError { get; } = readError;

        public ConcurrentDictionary<int, bool> UsedIgnore { get; } = new();

        public ConcurrentDictionary<int, bool> UsedLocaleSources { get; } = new();
    }
}
