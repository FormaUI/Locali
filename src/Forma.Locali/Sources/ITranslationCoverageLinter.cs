namespace Forma.Locali.Sources;

using Forma.Locali.Diagnostics;

/// <summary>Checks that every key a project writes is answered by the translation files its type already owns.</summary>
public interface ITranslationCoverageLinter
{
    /// <summary>Every key a call site writes that one of its type's translation files leaves out.</summary>
    /// <param name="localizationFiles">The project's translation files, already resolved to their types.</param>
    /// <param name="callSites">The call sites the source pass already found.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    ValueTask<IReadOnlyList<LintDiagnostic>> LintAsync(
        IReadOnlyList<TranslationFile> localizationFiles,
        IReadOnlyList<LocalizerCallSite> callSites,
        CancellationToken cancellationToken = default);
}
