namespace Forma.Locali.Sources;

using Forma.Locali.Diagnostics;
using Forma.Locali.Projects;

/// <summary>Checks a project's locale files read whole.</summary>
public interface IProjectLinter
{
    /// <summary>What the project rules report over one project.</summary>
    /// <param name="project">The project being checked.</param>
    /// <param name="localizationFiles">The locale files the project owns, which the target attributed.</param>
    /// <param name="callsites">The callsites the source pass found.</param>
    /// <param name="packs">Every locale pack of the run, resolved against its projects: the project's own among them where it is one, and the folders naming it where it is localized by one.</param>
    IReadOnlyList<LintingDiagnostic> Lint(LoadedProject project, IReadOnlyList<string> localizationFiles, IReadOnlyList<LocalizerLookup> callsites, IReadOnlyList<LocalePack> packs);
}
