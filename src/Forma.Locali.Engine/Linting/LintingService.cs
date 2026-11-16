namespace Forma.Locali.Linting;

using System.Collections.Concurrent;

using Forma.Locali.Cli;
using Forma.Locali.Configuration;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;
using Forma.Locali.Flow;
using Forma.Locali.Projects;
using Forma.Locali.Sources;

using Microsoft.Extensions.Options;

/// <summary>Lints the localization files and the source callsites the target names.</summary>
/// <param name="options">What the command line asked for.</param>
/// <param name="resolver">Turns the path into a file set.</param>
/// <param name="loader">Loads the projects the source files belong to.</param>
/// <param name="localizationFileLinter">Checks one localization file.</param>
/// <param name="sourcesLinter">Checks the keys written at callsites.</param>
/// <param name="projectLinter">Checks a project's locale files read whole.</param>
/// <param name="lintingConfiguration">The one <c>.locali.json</c> the run applies.</param>
/// <param name="localeFormat">Reads a locale pack's files as the runtime reads them.</param>
public sealed class LintingService(
    IOptions<LinterOptions> options,
    ILintingTargetResolver resolver,
    IProjectLoader loader,
    ILocalizationFileLinter localizationFileLinter,
    ISourceLinter sourcesLinter,
    IProjectLinter projectLinter,
    ILintingConfiguration lintingConfiguration,
    ILocaleFormat localeFormat) : ILintingService
{
    private static readonly IReadOnlySet<LintingCode> NoConfigIgnores = new HashSet<LintingCode>();

    private readonly LinterOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ILintingTargetResolver _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    private readonly IProjectLoader _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    private readonly ILocalizationFileLinter _linter = localizationFileLinter ?? throw new ArgumentNullException(nameof(localizationFileLinter));
    private readonly ISourceLinter _sources = sourcesLinter ?? throw new ArgumentNullException(nameof(sourcesLinter));
    private readonly IProjectLinter _projects = projectLinter ?? throw new ArgumentNullException(nameof(projectLinter));
    private readonly ILintingConfiguration _lintingConfiguration = lintingConfiguration ?? throw new ArgumentNullException(nameof(lintingConfiguration));
    private readonly ILocaleFormat _localeFormat = localeFormat ?? throw new ArgumentNullException(nameof(localeFormat));

    /// <inheritdoc />
    public async ValueTask<LintingResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var target = _resolver.Resolve(_options.Path);
        var diagnostics = new ConcurrentBag<LintingDiagnostic>();

        // Each file is read independently, so they overlap rather than queueing behind one another.
        await Parallel.ForEachAsync(
            target.LocalizationFiles,
            cancellationToken,
            async (file, token) =>
            {
                foreach (var diagnostic in await _linter.LintAsync(file, token).ConfigureAwait(false))
                {
                    diagnostics.Add(diagnostic);
                }
            }).ConfigureAwait(false);

        var (projectDiagnostics, coverage) = await LintProjectsAsync(target, cancellationToken).ConfigureAwait(false);
        foreach (var diagnostic in projectDiagnostics)
        {
            diagnostics.Add(diagnostic);
        }

        // The linting configuration is asked about every file it reads, even one with nothing to report, so
        // a glob matching only a clean file is not mistaken for one matching nothing at all.
        var configIgnores = target.LocalizationFiles
            .Concat(target.SourceFiles)
            .ToDictionary(Path.GetFullPath, _lintingConfiguration.GetIgnoredCodes, StringComparer.OrdinalIgnoreCase);

        foreach (var diagnostic in _lintingConfiguration.GetDiagnostics())
        {
            diagnostics.Add(diagnostic);
        }

        // The run reads one configuration, so another beneath the path it was given applies to nothing and is told so.
        foreach (var file in target.ConfigurationFiles)
        {
            if (!Path.GetFullPath(file).Equals(_lintingConfiguration.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new LintingDiagnostic(
                    file,
                    1,
                    1,
                    LintingCodes.UnreadConfiguration.DefaultSeverity,
                    LintingCodes.UnreadConfiguration,
                    _lintingConfiguration.FilePath is { } read
                        ? $"The run reads '{read}' and no other {LintingConfiguration.FileName}, so nothing in this file applies."
                        : $"The run reads the {LintingConfiguration.FileName} nearest above the path it was given, and none sits there, so nothing in this file applies."));
            }
        }

        var all = diagnostics.ToArray();

        bool IsIgnored(LintingDiagnostic diagnostic)
        {
            return configIgnores.GetValueOrDefault(Path.GetFullPath(diagnostic.Path), NoConfigIgnores).Contains(diagnostic.Code);
        }

        // What the linting configuration leaves out never reaches the result, whatever the severity.
        LintingDiagnostic[] reported =
        [
            .. all
                .Where(diagnostic => !IsIgnored(diagnostic) && diagnostic.Severity >= _options.MinimumSeverity)
                .OrderBy(diagnostic => diagnostic.Path, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Line)
                .ThenBy(diagnostic => diagnostic.Column),
        ];

        // What was left out is still counted per rule, whether the severity or the linting configuration is why.
        var leftOut = all.Where(diagnostic => IsIgnored(diagnostic) || diagnostic.Severity < _options.MinimumSeverity).ToArray();

        SuppressedRule[] suppressed =
        [
            .. leftOut
                .GroupBy(diagnostic => diagnostic.Code)
                .Select(rule => new SuppressedRule(rule.Key, rule.Count()))
                .OrderBy(rule => rule.Code.Id, StringComparer.Ordinal),
        ];

        return new LintingResult(reported, target.FileCount, suppressed, leftOut.Count(IsIgnored)) { Coverage = coverage };
    }

    private async ValueTask<(IReadOnlyList<LintingDiagnostic> Diagnostics, LookupCoverage Coverage)> LintProjectsAsync(LintingTarget target, CancellationToken cancellationToken)
    {
        if (target.Projects.Count == 0)
        {
            return ([], LookupCoverage.None);
        }

        // A project compiles whole because binding needs it, but only the named files are reported on.
        var wanted = target.SourceFiles.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var diagnostics = new List<LintingDiagnostic>();
        var linted = new List<(LoadedProject Project, SourceLintResult Sources)>();

        // Each project's callsites are linted as soon as it is loaded, while the loader builds the next one.
        await foreach (var project in _loader.LoadAsync(target.Projects, cancellationToken).ConfigureAwait(false))
        {
            linted.Add((project, _sources.Lint(project, wanted, cancellationToken)));
        }

        // The project rules wait for every project, since a locale pack's folders are resolved against the whole run and read for the projects they name.
        var projects = new LoadedProjects([.. linted.Select(entry => entry.Project)], target.IsSolution);
        var packs = LocalePackFiles.ResolveAll(_localeFormat.RuntimeFormat, projects, target.LocalizationFilesOf);
        var builtAssemblies = BuiltAssemblies.ResolveAll(projects);

        // A lookup's locale is followed across the projects of the run, so its types wait for every project as well, with the hints
        // each project carries and those of the packages the configuration names, found where the run's projects restored them.
        var hints = _lintingConfiguration.ReadHints(ProjectFolders(projects.All), PackageRoots(projects.All));
        var flow = new LocaleFlow(projects.All, hints.LocaleMembers);

        // A type's entries are read from its own assembly's project, so every project's files are resolved before any rule reads them.
        var localizations = RunLocalizations.Resolve(_localeFormat.RuntimeFormat, projects.All, target.LocalizationFilesOf, packs, builtAssemblies);

        // A key passed through a parameter is checked at each call writing it, in the project holding the call, and is left unread where
        // a call writes it otherwise.
        var forwarded = flow.Forward([.. linted.SelectMany(entry => entry.Sources.ForwardedKeys.Select(key => (entry.Project, key)))]);
        IReadOnlyList<LocalizerLookup> Forwarded(LoadedProject project)
        {
            return [.. forwarded.ByProject.GetValueOrDefault(project.Path, []).Where(site => wanted.Contains(site.Key.Location.Path))];
        }

        diagnostics.AddRange(_sources.Lint([.. linted.SelectMany(entry => Forwarded(entry.Project))], [.. forwarded.Unforwarded.Where(key => wanted.Contains(key.Key.Path))]));

        var resolved = new List<LocalizerLookup>();
        foreach (var (project, sources) in linted)
        {
            IReadOnlyList<LocalizerLookup> callsites = [.. flow.Resolve(project, sources.Callsites), .. Forwarded(project)];
            resolved.AddRange(callsites);
            diagnostics.AddRange(sources.Diagnostics);
            diagnostics.AddRange(_projects.Lint(project, target.LocalizationFilesOf(project.Path), callsites, packs, localizations));
        }

        diagnostics.AddRange(hints.Diagnostics);
        diagnostics.AddRange(flow.GetDiagnostics());

        return (diagnostics, Coverage(resolved, localizations));
    }

    // A lookup is checked against each certain type whose entries the run reads, from the project building the type's assembly;
    // what kept it short of that is named for the path that ended, or for the alternatives or the entries it could not use.
    private static LookupCoverage Coverage(List<LocalizerLookup> lookups, RunLocalizations localizations)
    {
        int fullyChecked = 0, partiallyChecked = 0, notChecked = 0, byAnalysis = 0, byVerifiedHint = 0, byHint = 0;
        var reasons = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var lookup in lookups)
        {
            var certain = lookup.Origins.Where(origin => !origin.IsApproximate).ToArray();
            var read = certain.Where(origin => origin.Type.ContainingAssembly?.Name is { Length: > 0 } name && localizations.ForAssembly(name) is not null).ToArray();

            var shortfalls = new SortedSet<string>(lookup.Gaps.Select(gap => Camel(gap.Kind.ToString())), StringComparer.Ordinal);
            if (certain.Length < lookup.Origins.Count)
            {
                shortfalls.Add("approximate");
            }

            if (read.Length < certain.Length)
            {
                shortfalls.Add("unavailableEntries");
            }

            // A lookup found without a bound compilation, or in code no instance runs, was never followed to a type.
            if (read.Length == 0 && shortfalls.Count == 0)
            {
                shortfalls.Add("notFollowed");
            }

            foreach (var reason in shortfalls)
            {
                reasons[reason] = reasons.GetValueOrDefault(reason) + 1;
            }

            if (read.Length == 0)
            {
                notChecked++;
                continue;
            }

            if (shortfalls.Count == 0)
            {
                fullyChecked++;
            }
            else
            {
                partiallyChecked++;
            }

            byAnalysis += read.Any(origin => origin.Evidence == LocaleEvidence.Analysis) ? 1 : 0;
            byVerifiedHint += read.Any(origin => origin.Evidence == LocaleEvidence.VerifiedHint) ? 1 : 0;
            byHint += read.Any(origin => origin.Evidence == LocaleEvidence.Hint) ? 1 : 0;
        }

        return new LookupCoverage(lookups.Count, fullyChecked, partiallyChecked, notChecked, new LookupEvidence(byAnalysis, byVerifiedHint, byHint), reasons);
    }

    private static string Camel(string name)
        => string.Concat(char.ToLowerInvariant(name[0]).ToString(), name[1..]);

    private static string[] ProjectFolders(IReadOnlyList<LoadedProject> projects)
        => [.. projects.Select(project => Path.GetDirectoryName(Path.GetFullPath(project.Path))!).Distinct(StringComparer.OrdinalIgnoreCase)];

    private static Dictionary<string, IReadOnlyCollection<string>> PackageRoots(IReadOnlyList<LoadedProject> projects)
        => projects
            .SelectMany(project => project.PackageRoots)
            .GroupBy(root => root.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                package => package.Key,
                IReadOnlyCollection<string> (package) => [.. package.Select(root => root.Value).Distinct(StringComparer.OrdinalIgnoreCase)],
                StringComparer.OrdinalIgnoreCase);
}
