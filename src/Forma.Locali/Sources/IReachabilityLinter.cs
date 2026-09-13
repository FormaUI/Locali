namespace Forma.Locali.Sources;

using Forma.Locali.Diagnostics;

/// <summary>Checks that every translation a project's files declare is one some type of the project reads.</summary>
public interface IReachabilityLinter
{
    /// <summary>Every file, and every section of a file, that no type of the project reads.</summary>
    /// <param name="translations">The project's translation files, resolved against its types.</param>
    IReadOnlyList<LintDiagnostic> Lint(ProjectTranslations translations);
}
