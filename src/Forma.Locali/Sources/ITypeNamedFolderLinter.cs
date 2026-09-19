namespace Forma.Locali.Sources;

using Forma.Locali.Diagnostics;

/// <summary>Checks that no folder of a project's locale files is named for a type.</summary>
public interface ITypeNamedFolderLinter
{
    /// <summary>Every file named for its culture alone whose folder is named for a type of the project.</summary>
    /// <param name="localization">The project's locale files, resolved against its types.</param>
    IReadOnlyList<LintingDiagnostic> Lint(ProjectLocalization localization);
}
