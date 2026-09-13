namespace Forma.Locali.Linting;

using System.Collections.Concurrent;

using Forma.Locali.Cli;
using Forma.Locali.Diagnostics;
using Forma.Locali.Projects;
using Forma.Locali.Sources;

using Microsoft.Extensions.Options;

/// <summary>Lints the localization files and the source call sites the target names.</summary>
/// <param name="options">What the command line asked for.</param>
/// <param name="resolver">Turns the path into a file set.</param>
/// <param name="loader">Loads the projects the source files belong to.</param>
/// <param name="localizationFileLinter">Checks one localization file.</param>
/// <param name="sourcesLinter">Checks the keys written at call sites.</param>
/// <param name="reachabilityLinter">Checks that a project's translation files can still be found by type.</param>
/// <param name="coverageLinter">Checks that a project's keys are answered by the translation files it owns.</param>
public sealed class LintService(
    IOptions<LinterOptions> options,
    ILintTargetResolver resolver,
    IProjectLoader loader,
    ILocalizationFileLinter localizationFileLinter,
    ISourceLinter sourcesLinter,
    IReachabilityLinter reachabilityLinter,
    ITranslationCoverageLinter coverageLinter) : ILintService
{
    private readonly LinterOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ILintTargetResolver _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    private readonly IProjectLoader _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    private readonly ILocalizationFileLinter _linter = localizationFileLinter ?? throw new ArgumentNullException(nameof(localizationFileLinter));
    private readonly ISourceLinter _sources = sourcesLinter ?? throw new ArgumentNullException(nameof(sourcesLinter));
    private readonly IReachabilityLinter _reachability = reachabilityLinter ?? throw new ArgumentNullException(nameof(reachabilityLinter));
    private readonly ITranslationCoverageLinter _coverage = coverageLinter ?? throw new ArgumentNullException(nameof(coverageLinter));

    /// <inheritdoc />
    public async ValueTask<LintResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var target = _resolver.Resolve(_options.Path, _options.Excludes);
        var diagnostics = new ConcurrentBag<LintDiagnostic>();

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

        // A diagnostic below the severity leaves the result entirely, so it cannot reach the exit code either.
        LintDiagnostic[] reported =
        [
            .. diagnostics
                .Where(diagnostic => diagnostic.Severity >= _options.MinimumSeverity)
                .OrderBy(diagnostic => diagnostic.Path, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Line)
                .ThenBy(diagnostic => diagnostic.Column),
        ];

        // What was left out is still counted per rule, so a default run says what it is not showing.
        SuppressedRule[] suppressed =
        [
            .. diagnostics
                .Where(diagnostic => diagnostic.Severity < _options.MinimumSeverity)
                .GroupBy(diagnostic => diagnostic.Code)
                .Select(rule => new SuppressedRule(rule.Key, rule.Count()))
                .OrderBy(rule => rule.Code.Id, StringComparer.Ordinal),
        ];

        return new LintResult(reported, target.FileCount, suppressed);
    }

    private async ValueTask<IReadOnlyList<LintDiagnostic>> LintProjectsAsync(LintTarget target, CancellationToken cancellationToken)
    {
        if (target.Projects.Count == 0)
        {
            return [];
        }

        // A project compiles whole because binding needs it, but only the named files are reported on.
        var wanted = target.SourceFiles.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var diagnostics = new List<LintDiagnostic>();

        foreach (var project in await _loader.LoadAsync(target.Projects, cancellationToken).ConfigureAwait(false))
        {
            var sources = _sources.Lint(project, wanted, cancellationToken);
            diagnostics.AddRange(sources.Diagnostics);

            // Both rules over a project's translation files read the same resolution, so it is made once.
            if (TranslationFiles.Resolve(project, target.LocalizationFilesOf(project.Path)) is { } translations)
            {
                diagnostics.AddRange(_reachability.Lint(translations));
                diagnostics.AddRange(_coverage.Lint(translations, sources.CallSites));
            }
        }

        return diagnostics;
    }
}
