namespace Forma.Locali.Linting;

using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>Checks a file's metadata entry against the schema, strictly where the runtime is permissive.</summary>
public interface ISchemaLinter
{
    /// <summary>Everything the rules find wrong with the file, in the order the file would be read.</summary>
    /// <param name="document">The file to check.</param>
    /// <param name="expectedCulture">The culture the file's own name gives it.</param>
    IReadOnlyList<LintDiagnostic> Lint(LocalizationDocument document, string? expectedCulture);
}
