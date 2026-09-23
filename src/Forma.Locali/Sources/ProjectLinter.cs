namespace Forma.Locali.Sources;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Projects;
using Forma.Locali.Rules;

/// <summary>Resolves a project's locale files once, then lets every project rule read them.</summary>
/// <param name="checkers">The rules to run over each project.</param>
public sealed class ProjectLinter(IEnumerable<IProjectRuleChecker> checkers) : IProjectLinter
{
    private readonly IEnumerable<IProjectRuleChecker> _checkers = checkers ?? throw new ArgumentNullException(nameof(checkers));

    /// <inheritdoc />
    public IReadOnlyList<LintingDiagnostic> Lint(LoadedProject project, IReadOnlyList<string> localizationFiles, IReadOnlyList<LocalizerLookup> callsites, LoadedProjects projects)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(localizationFiles);
        ArgumentNullException.ThrowIfNull(callsites);
        ArgumentNullException.ThrowIfNull(projects);

        var context = new ProjectContext(
            project,
            localizationFiles,
            ProjectLocaleFiles.Resolve(project, localizationFiles),
            callsites,
            HostLocaleFiles.Resolve(project, localizationFiles),
            LocalePackFiles.Resolve(project, localizationFiles, projects));

        var refusal = default(LocaleConfigException);

        foreach (var checker in _checkers)
        {
            try
            {
                checker.Check(context);
            }
            catch (LocaleConfigException exception)
            {
                refusal ??= exception;
            }
        }

        // The runtime refuses every lookup in an assembly whose embedded config it cannot place, so the refusal is reported once, at the project.
        return refusal is null
            ? context.Reported
            : [.. context.Reported, new LintingDiagnostic(project.Path, 1, 1, LintingCodes.MalformedLocaleConfig.DefaultSeverity, LintingCodes.MalformedLocaleConfig, refusal.Message)];
    }
}
