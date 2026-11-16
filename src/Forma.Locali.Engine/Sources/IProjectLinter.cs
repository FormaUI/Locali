namespace Forma.Locali.Sources;

using Forma.Locali.Diagnostics;
using Forma.Locali.Projects;

/// <summary>Checks a project's locale files read whole.</summary>
public interface IProjectLinter
{
    /// <summary>What every project rule reports over one project.</summary>
    /// <param name="project">The project to check.</param>
    /// <param name="localizationFiles">The locale files the project owns, which the target attributed.</param>
    /// <param name="callsites">The callsites the source pass found in the project.</param>
    /// <param name="packs">Every locale pack of the run.</param>
    /// <param name="run">The locale files of every project of the run, resolved once.</param>
    IReadOnlyList<LintingDiagnostic> Lint(LoadedProject project, IReadOnlyList<string> localizationFiles, IReadOnlyList<LocalizerLookup> callsites, IReadOnlyList<LocalePack> packs, RunLocalizations run);
}
