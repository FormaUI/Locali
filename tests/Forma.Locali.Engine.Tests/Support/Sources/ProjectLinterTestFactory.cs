namespace Forma.Locali.Tests.Support.Sources;

using Forma.Locali.Diagnostics;
using Forma.Locali.Projects;
using Forma.Locali.Rules;
using Forma.Locali.Rules.Projects;
using Forma.Locali.Sources;

/// <summary>Builds the project rules the linter ships with, and runs one over a project.</summary>
public static class ProjectLinterTestFactory
{
    /// <summary>Every project rule the linter ships with.</summary>
    /// <param name="lintingConfiguration">The linting configuration the rules that read one are given; unless given, one over the temporary folder, which no <c>.locali.json</c> sits above, so it names nothing.</param>
    public static IReadOnlyList<IProjectRuleChecker> Checkers(ILintingConfiguration? lintingConfiguration = null)
        =>
        [
            new LocaleConfigPlacementChecker(),
            new RootNamespaceWithoutTypesChecker(),
            new UnreachableLocalizationChecker(),
            new LocalizationCoverageChecker(),
            new TypeLocalizationChecker(),
            new TypeNamedFolderChecker(),
            new CultureCoverageChecker(),
            new EntryConflictChecker(),
            new AmbiguousNameChecker(),
            new CultureInAnotherCaseChecker(),
            new SecondCultureInNameChecker(),
            new CultureOutsideSatelliteChecker(),
            new LocalePackAssemblyChecker(),
            new LocaleSourceChecker(lintingConfiguration ?? new LintingConfiguration(Path.GetTempPath()), LocaleFormatTestFactory.Json),
            new CultureInRootNamespaceChecker(),
        ];

    /// <summary>What one project rule reports over a project's files, resolved as the linter resolves them.</summary>
    /// <param name="checker">The rule to run.</param>
    /// <param name="project">The project the files belong to.</param>
    /// <param name="localizationFiles">The locale files the project owns.</param>
    /// <param name="callsites">The callsites the source pass found, none unless given.</param>
    /// <param name="packs">Every locale pack of the run, none unless given.</param>
    /// <param name="builtAssemblies">Every assembly a project of the run builds, none unless given.</param>
    public static IReadOnlyList<LintingDiagnostic> Check(
        IProjectRuleChecker checker,
        LoadedProject project,
        IReadOnlyList<string> localizationFiles,
        IReadOnlyList<LocalizerLookup>? callsites = null,
        IReadOnlyList<LocalePack>? packs = null,
        IReadOnlyList<BuiltAssembly>? builtAssemblies = null)
    {
        ArgumentNullException.ThrowIfNull(checker);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(localizationFiles);

        var run = RunLocalizations.Resolve(LocaleFormatTestFactory.Json.RuntimeFormat, [project], _ => localizationFiles, packs ?? [], builtAssemblies ?? []);

        return new ProjectLinter([checker]).Lint(project, localizationFiles, callsites ?? [], packs ?? [], run);
    }
}
