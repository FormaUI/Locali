namespace Forma.Locali.Linting;

using System.Text;

using Forma.Framework.Localization.Files;
using Forma.Locali.Configuration;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>Reads a file and runs the schema rules over it, or holds a <c>.locale.json</c> among the files to the runtime's own rule.</summary>
/// <param name="schema">Runs the rules once the file is readable.</param>
public sealed class LocalizationFileLinter(ISchemaLinter schema) : ILocalizationFileLinter
{
    private static readonly byte[] MarkerUtf8 = Encoding.UTF8.GetBytes(LocalizationSchema.Marker);

    private readonly ISchemaLinter _schema = schema ?? throw new ArgumentNullException(nameof(schema));

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<LintingDiagnostic>> LintAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        byte[] content;
        try
        {
            content = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            return [Unreadable(filePath, exception.Message)];
        }
        catch (UnauthorizedAccessException exception)
        {
            return [Unreadable(filePath, exception.Message)];
        }

        if (Path.GetFileName(filePath).Equals(LocaleConfig.FileName, StringComparison.OrdinalIgnoreCase))
        {
            return LintConfig(filePath, content);
        }

        var culture = LocalizationFileName.GetCulture(filePath);

        if (!LocalizationDocument.TryRead(filePath, content, out var document, out var error))
        {
            // Unreadable JSON is only this linter's business when the file claims to be a localization file.
            return HasMarker(content) || culture is not null
                ? [Unreadable(filePath, error ?? "the file is not readable JSON")]
                : [];
        }

        if (!document!.Entries.Any(static entry => entry.IsTopLevel && entry.Name == LocalizationSchema.Marker) && culture is null)
        {
            return [];
        }

        return _schema.Lint(document, culture);
    }

    // The runtime refuses a config file it cannot read and passes over a key it does not know; both are reported here.
    private static IReadOnlyList<LintingDiagnostic> LintConfig(string filePath, byte[] content)
    {
        if (!LocaleConfigFile.TryRead(filePath, content, out var config, out var error))
        {
            return [new LintingDiagnostic(filePath, 1, 1, LintingCodes.MalformedLocaleConfig.DefaultSeverity, LintingCodes.MalformedLocaleConfig, $"The file is not readable JSON: {error}.")];
        }

        return [.. config!.Malformed.Select(malformed => config.Diagnose(malformed.Offset, LintingCodes.MalformedLocaleConfig, malformed.Reason))];
    }

    private static bool HasMarker(byte[] content)
        => content.AsSpan().IndexOf(MarkerUtf8) >= 0;

    private static LintingDiagnostic Unreadable(string filePath, string message)
        => new(filePath, 1, 1, LintingCodes.InvalidJson.DefaultSeverity, LintingCodes.InvalidJson, message);
}
