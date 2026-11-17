namespace Forma.Locali.Sources;

using Forma.Locali.Diagnostics;
using Forma.Locali.Projects;

/// <summary>Checks the keys written at a project's localization callsites.</summary>
public interface ISourceLinter
{
    /// <summary>What the project's callsites produced, and the sites themselves.</summary>
    /// <param name="project">The project to read.</param>
    /// <param name="files">The source files the run asked for, or empty for all of them.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    SourceLintResult Lint(
        LoadedProject project,
        IReadOnlyCollection<string> files,
        CancellationToken cancellationToken = default);

    /// <summary>What the callsite rules report for the lookups keys passed through parameters make at the calls writing them.</summary>
    /// <param name="forwarded">The lookups the calls make.</param>
    IReadOnlyList<LintingDiagnostic> Lint(IReadOnlyList<LocalizerLookup> forwarded);

    /// <summary>Lints a project's calls to the methods a hint states look a parameter up as a key, which no project of the run builds, as lookups of their keys.</summary>
    /// <param name="project">The loaded project.</param>
    /// <param name="files">The files the run reports on, as full paths.</param>
    /// <param name="keyed">The methods the hints state look a key up.</param>
    /// <param name="cancellationToken">Stops the walk.</param>
    SourceLintResult LintKeyed(LoadedProject project, IReadOnlyCollection<string> files, KeyedMethods keyed, CancellationToken cancellationToken = default);
}
