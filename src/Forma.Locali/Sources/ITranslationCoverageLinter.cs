namespace Forma.Locali.Sources;

using Forma.Locali.Diagnostics;

/// <summary>Checks that every key a project writes is answered in each culture its type is already translated for.</summary>
public interface ITranslationCoverageLinter
{
    /// <summary>Every key a call site writes that a culture translating its type leaves out.</summary>
    /// <param name="translations">The project's translation files, resolved against its types.</param>
    /// <param name="callSites">The call sites the source pass already found.</param>
    IReadOnlyList<LintDiagnostic> Lint(ProjectTranslations translations, IReadOnlyList<LocalizerCallSite> callSites);
}
