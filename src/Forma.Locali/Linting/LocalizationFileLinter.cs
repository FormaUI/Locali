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
    public async ValueTask<IReadOnlyList<LintDiagnostic>> LintAsync(string file, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(file);

        byte[] content;
        try
        {
            content = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            return [Unreadable(file, exception.Message)];
        }
        catch (UnauthorizedAccessException exception)
        {
            return [Unreadable(file, exception.Message)];
        }

        var culture = LocalizationFileName.CultureOf(file);

        if (!LocalizationDocument.TryRead(file, content, out var document, out var error))
        {
            // Unreadable JSON is only this linter's business when the file claims to be a localization file.
            return HasMarker(content) || culture is not null
                ? [Unreadable(file, error ?? "the file is not readable JSON")]
                : [];
        }

        if (!document!.Entries.Any(static entry => entry.IsTopLevel && entry.Name == LocalizationSchema.Marker) && culture is null)
        {
            return [];
        }

        // A file leaves the rules its tools entry lists out of its own report; the entry itself is still judged.
        var ignored = IgnoredRules.Read(document);

        return [.. _schema.Lint(document, culture).Where(diagnostic => !ignored.Ignores(diagnostic.Code))];
    }

    private static bool HasMarker(byte[] content)
        => content.AsSpan().IndexOf(MarkerUtf8) >= 0;

    private static LintDiagnostic Unreadable(string file, string message)
        => new(file, 1, 1, LintCodes.InvalidJson.DefaultSeverity, LintCodes.InvalidJson, message);
}
