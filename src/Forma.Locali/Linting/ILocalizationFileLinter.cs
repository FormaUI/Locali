namespace Forma.Locali.Linting;

/// <summary>Checks one file.</summary>
public interface ILocalizationFileLinter
{
    /// <summary>What a file produces, which is empty for JSON that is not a localization file.</summary>
    /// <param name="file">The file to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    ValueTask<LocalizationFileLintResult> LintAsync(string file, CancellationToken cancellationToken = default);
}
