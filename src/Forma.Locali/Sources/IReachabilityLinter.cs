namespace Forma.Locali.Sources;

using Forma.Locali.Diagnostics;

/// <summary>Checks that every translation a project's files declare is one some type of the project reads.</summary>
public interface IReachabilityLinter
{
    /// <summary>Every file, and every section of a file, that no type of the project reads.</summary>
    /// <param name="localization">The project's locale files, resolved against its types.</param>
    IReadOnlyList<LintingDiagnostic> Lint(ProjectLocalization localization);
}
