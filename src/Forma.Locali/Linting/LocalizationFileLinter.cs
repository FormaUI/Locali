namespace Forma.Locali.Linting;

using System.Text;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;
using Forma.Locali.Rules;

/// <summary>Reads a file and runs the schema rules over it.</summary>
/// <param name="schema">Runs the rules once the file is readable.</param>
public sealed class LocalizationFileLinter(ISchemaLinter schema) : ILocalizationFileLinter
{
    private static readonly byte[] MarkerUtf8 = Encoding.UTF8.GetBytes(LocalizationSchema.Marker);

    private readonly ISchemaLinter _schema = schema ?? throw new ArgumentNullException(nameof(schema));

    /// <inheritdoc />
    public async ValueTask<LocalizationFileLintResult> LintAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        byte[] content;
        try
        {
            content = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            return new LocalizationFileLintResult([Unreadable(filePath, exception.Message)], IgnoredRules.None);
        }
        catch (UnauthorizedAccessException exception)
        {
            return new LocalizationFileLintResult([Unreadable(filePath, exception.Message)], IgnoredRules.None);
        }

        var culture = LocalizationFileName.GetCulture(filePath);

        if (!LocalizationDocument.TryRead(filePath, content, out var document, out var error))
        {
            // Unreadable JSON is only this linter's business when the file claims to be a localization file.
            return HasMarker(content) || culture is not null
                ? new LocalizationFileLintResult([Unreadable(filePath, error ?? "the file is not readable JSON")], IgnoredRules.None)
                : LocalizationFileLintResult.Empty;
        }

        if (!document!.Entries.Any(static entry => entry.IsTopLevel && entry.Name == LocalizationSchema.Marker) && culture is null)
        {
            return LocalizationFileLintResult.Empty;
        }

        // What a file's tools entry leaves out is left to the caller to apply, so a run can still count and brief it.
        return new LocalizationFileLintResult(_schema.Lint(document, culture), IgnoredRules.Read(document));
    }

    private static bool HasMarker(byte[] content)
        => content.AsSpan().IndexOf(MarkerUtf8) >= 0;

    private static LintingDiagnostic Unreadable(string filePath, string message)
        => new(filePath, 1, 1, LintingCodes.InvalidJson.DefaultSeverity, LintingCodes.InvalidJson, message);
}
