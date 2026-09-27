namespace Forma.Locali.Linting;

using System.Collections.Concurrent;

using Forma.Locali.Cli;
using Forma.Locali.Configuration;
using Forma.Locali.Diagnostics;
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
public sealed class LintingService(
    IOptions<LinterOptions> options,
    ILintingTargetResolver resolver,
    IProjectLoader loader,
    ILocalizationFileLinter localizationFileLinter,
    ISourceLinter sourcesLinter,
    IProjectLinter projectLinter,
    ILintingConfiguration lintingConfiguration) : ILintingService
{
    private static readonly IReadOnlySet<LintingCode> NoConfigIgnores = new HashSet<LintingCode>();

    private readonly LinterOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ILintingTargetResolver _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    private readonly IProjectLoader _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    private readonly ILocalizationFileLinter _linter = localizationFileLinter ?? throw new ArgumentNullException(nameof(localizationFileLinter));
    private readonly ISourceLinter _sources = sourcesLinter ?? throw new ArgumentNullException(nameof(sourcesLinter));
    private readonly IProjectLinter _projects = projectLinter ?? throw new ArgumentNullException(nameof(projectLinter));
    private readonly ILintingConfiguration _lintingConfiguration = lintingConfiguration ?? throw new ArgumentNullException(nameof(lintingConfiguration));

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

        foreach (var diagnostic in await LintProjectsAsync(target, cancellationToken).ConfigureAwait(false))
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

        return new LintingResult(reported, target.FileCount, suppressed, leftOut.Count(IsIgnored));
    }

    private async ValueTask<IReadOnlyList<LintingDiagnostic>> LintProjectsAsync(LintingTarget target, CancellationToken cancellationToken)
    {
        if (target.Projects.Count == 0)
        {
            return [];
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
        var packs = LocalePackFiles.ResolveAll(projects, target.LocalizationFilesOf);

        foreach (var (project, sources) in linted)
        {
            diagnostics.AddRange(sources.Diagnostics);
            diagnostics.AddRange(_projects.Lint(project, target.LocalizationFilesOf(project.Path), sources.Callsites, packs));
        }

        return diagnostics;
    }
}
