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

    /// <summary>What the callsite rules report for the lookups keys passed through parameters make at the calls writing them, and for the keys no path writes as text.</summary>
    /// <param name="forwarded">The lookups the calls make.</param>
    /// <param name="unforwarded">The keys some call writes otherwise, or that no call of the run writes at all.</param>
    IReadOnlyList<LintingDiagnostic> Lint(IReadOnlyList<LocalizerLookup> forwarded, IReadOnlyList<ForwardedKey> unforwarded);
}
