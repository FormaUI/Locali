namespace Forma.Locali.Linting;

using System.Collections.Concurrent;

using Forma.Framework.Localization.Files;
using Forma.Locali.Cli;
using Forma.Locali.Configuration;
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
/// <param name="reachabilityLinter">Checks that a project's locale files can still be found by type.</param>
/// <param name="coverageLinter">Checks that a project's keys are answered by the locale files it owns.</param>
/// <param name="lintingConfiguration">What the linting configuration's <c>.locali.json</c> files leave out.</param>
public sealed class LintingService(
    IOptions<LinterOptions> options,
    ILintingTargetResolver resolver,
    IProjectLoader loader,
    ILocalizationFileLinter localizationFileLinter,
    ISourceLinter sourcesLinter,
    IReachabilityLinter reachabilityLinter,
    ILocalizationCoverageLinter coverageLinter,
    ILintingConfiguration lintingConfiguration) : ILintingService
{
    private static readonly IReadOnlySet<LintingCode> NoConfigIgnores = new HashSet<LintingCode>();

    private readonly LinterOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ILintingTargetResolver _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    private readonly IProjectLoader _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    private readonly ILocalizationFileLinter _linter = localizationFileLinter ?? throw new ArgumentNullException(nameof(localizationFileLinter));
    private readonly ISourceLinter _sources = sourcesLinter ?? throw new ArgumentNullException(nameof(sourcesLinter));
    private readonly IReachabilityLinter _reachability = reachabilityLinter ?? throw new ArgumentNullException(nameof(reachabilityLinter));
    private readonly ILocalizationCoverageLinter _coverage = coverageLinter ?? throw new ArgumentNullException(nameof(coverageLinter));
    private readonly ILintingConfiguration _lintingConfiguration = lintingConfiguration ?? throw new ArgumentNullException(nameof(lintingConfiguration));

    /// <inheritdoc />
    public async ValueTask<LintingResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var target = _resolver.Resolve(_options.Path, _options.Excludes);
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

        foreach (var project in await _loader.LoadAsync(target.Projects, cancellationToken).ConfigureAwait(false))
        {
            var sources = _sources.Lint(project, wanted, cancellationToken);
            diagnostics.AddRange(sources.Diagnostics);
            var localizationFiles = target.LocalizationFilesOf(project.Path);
            diagnostics.AddRange(LintConfigPlacement(project, localizationFiles));

            // Both rules over a project's locale files read the same resolution, so it is made once.
            if (ProjectLocaleFiles.Resolve(project, localizationFiles) is { } localization)
            {
                try
                {
                    diagnostics.AddRange(_reachability.Lint(localization));
                    diagnostics.AddRange(_coverage.Lint(localization, sources.CallSites));
                }
                catch (LocaleConfigException exception)
                {
                    // The runtime refuses every lookup in an assembly whose embedded config it cannot place, so none of its translations can be judged.
                    diagnostics.Add(new LintingDiagnostic(project.Path, 1, 1, LintingCodes.MalformedLocaleConfig.DefaultSeverity, LintingCodes.MalformedLocaleConfig, exception.Message));
                }
            }
        }

        return diagnostics;
    }

    // An assembly's config file is embedded at its project's root; the runtime reads one embedded elsewhere as the assembly's all the same, and one in a host's folder not at all.
    private static IEnumerable<LintingDiagnostic> LintConfigPlacement(LoadedProject project, IReadOnlyList<string> localizationFiles)
    {
        var projectFolder = Path.GetDirectoryName(Path.GetFullPath(project.Path))!;

        foreach (var file in localizationFiles)
        {
            var fullPath = Path.GetFullPath(file);
            if (!Path.GetFileName(fullPath).Equals(LocaleConfig.FileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (project.EmbeddedResourceNames is { } resourceNames && !resourceNames.ContainsKey(fullPath))
            {
                yield return DiagnoseConfig(fullPath, "The project does not embed this file, so the runtime never reads it, and a host's folder is never read for config; an assembly's config file is embedded at its project's root.");
            }
            else if (!Path.GetDirectoryName(fullPath)!.Equals(projectFolder, StringComparison.OrdinalIgnoreCase))
            {
                yield return DiagnoseConfig(fullPath, $"This file sits beneath '{Path.GetRelativePath(projectFolder, Path.GetDirectoryName(fullPath)!)}'; an assembly's config file sits at its project's root, beside '{Path.GetFileName(project.Path)}'.");
            }
        }
    }

    private static LintingDiagnostic DiagnoseConfig(string path, string message)
        => new(path, 1, 1, LintingCodes.MalformedLocaleConfig.DefaultSeverity, LintingCodes.MalformedLocaleConfig, message);
}
