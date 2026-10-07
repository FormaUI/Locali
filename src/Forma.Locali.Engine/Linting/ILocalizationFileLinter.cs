namespace Forma.Locali.Linting;

using Forma.Locali.Diagnostics;

/// <summary>Checks one file.</summary>
public interface ILocalizationFileLinter
{
    /// <summary>What a file reports, which is nothing for JSON that is neither a localization file nor the runtime's config file.</summary>
    /// <param name="filePath">The file to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    ValueTask<IReadOnlyList<LintingDiagnostic>> LintAsync(string filePath, CancellationToken cancellationToken = default);
}
