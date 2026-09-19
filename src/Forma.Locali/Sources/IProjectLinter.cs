namespace Forma.Locali.Sources;

using Forma.Locali.Diagnostics;
using Forma.Locali.Projects;

/// <summary>Checks a project's locale files read whole.</summary>
public interface IProjectLinter
{
    /// <summary>What the project rules report over one project.</summary>
    /// <param name="project">The project being checked.</param>
    /// <param name="localizationFiles">The locale files the project owns, which the target attributed.</param>
    /// <param name="callSites">The call sites the source pass found.</param>
    IReadOnlyList<LintingDiagnostic> Lint(LoadedProject project, IReadOnlyList<string> localizationFiles, IReadOnlyList<LocalizerLookup> callSites);
}
