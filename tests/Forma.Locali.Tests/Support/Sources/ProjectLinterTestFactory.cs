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
    public static IReadOnlyList<IProjectRuleChecker> Checkers()
        =>
        [
            new LocaleConfigPlacementChecker(),
            new RootNamespaceWithoutTypesChecker(),
            new UnreachableLocalizationChecker(),
            new LocalizationCoverageChecker(),
            new DuplicateAnchorFileChecker(),
            new TypeNamedFolderChecker(),
            new LocalePackFolderChecker(),
        ];

    /// <summary>What one project rule reports over a project's files, resolved as the linter resolves them.</summary>
    /// <param name="checker">The rule to run.</param>
    /// <param name="project">The project the files belong to.</param>
    /// <param name="localizationFiles">The locale files the project owns.</param>
    /// <param name="callsites">The callsites the source pass found, none unless given.</param>
    /// <param name="projects">Every project the run loaded, the project alone as no solution unless given.</param>
    public static IReadOnlyList<LintingDiagnostic> Check(
        IProjectRuleChecker checker,
        LoadedProject project,
        IReadOnlyList<string> localizationFiles,
        IReadOnlyList<LocalizerLookup>? callsites = null,
        LoadedProjects? projects = null)
    {
        ArgumentNullException.ThrowIfNull(checker);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(localizationFiles);

        return new ProjectLinter([checker]).Lint(project, localizationFiles, callsites ?? [], projects ?? new LoadedProjects([project], IsWholeSolution: false));
    }

    /// <summary>What one project rule reports over files already placed, read as the host's folder.</summary>
    /// <param name="checker">The rule to run.</param>
    /// <param name="files">The placed files.</param>
    public static IReadOnlyList<LintingDiagnostic> Check(IProjectRuleChecker checker, IReadOnlyList<PlacedLocaleFile> files)
    {
        ArgumentNullException.ThrowIfNull(checker);
        ArgumentNullException.ThrowIfNull(files);

        var context = new ProjectContext(new LoadedProject("Scratch.csproj", null, false, false), [], null, [], files);
        checker.Check(context);

        return context.Reported;
    }
}
