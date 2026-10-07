namespace Forma.Locali.Linting;

using Forma.Framework.Localization.Files;
using Forma.Framework.Localization.Parser;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;
using Forma.Locali.Rules;

/// <summary>Establishes a file's metadata, then lets every rule read it.</summary>
/// <param name="checkers">The rules to run once the metadata is readable.</param>
public sealed class SchemaLinter(IEnumerable<ILocalizationFileRuleChecker> checkers) : ISchemaLinter
{
    private readonly IEnumerable<ILocalizationFileRuleChecker> _checkers = checkers ?? throw new ArgumentNullException(nameof(checkers));

    /// <inheritdoc />
    public IReadOnlyList<LintingDiagnostic> Lint(LocalizationDocument document, string? expectedCulture)
    {
        ArgumentNullException.ThrowIfNull(document);

        var diagnostics = new List<LintingDiagnostic>();

        // Every rule reads the metadata, so a file whose metadata cannot be read reports that once
        // rather than letting each rule report the same absence again.
        var context = Establish(document, expectedCulture, diagnostics);
        if (context is null)
        {
            return diagnostics;
        }

        foreach (var checker in _checkers)
        {
            checker.Check(context);
        }

        return [.. diagnostics, .. context.Reported];
    }

    private static LocalizationFileContext? Establish(
        LocalizationDocument document,
        string? expectedCulture,
        List<LintingDiagnostic> diagnostics)
    {
        var index = IndexOfMarker(document);
        if (index < 0)
        {
            diagnostics.Add(document.Diagnose(
                0,
                LintingCodes.MissingMarker,
                $"The file carries no '{LocalizationSchema.Marker}' entry, so it cannot be read as a localization file."));

            return null;
        }

        var marker = document.Entries[index];
        if (marker.PropertyIndex != 0)
        {
            // The runtime is order-agnostic about presence alone, so a marker sitting elsewhere still loads.
            diagnostics.Add(document.Diagnose(
                marker.NameOffset,
                LintingCodes.MarkerNotFirst,
                $"'{LocalizationSchema.Marker}' is entry {marker.PropertyIndex + 1}; it has to be the first."));
        }

        if (marker.Value is not { } value)
        {
            diagnostics.Add(document.Diagnose(
                marker.ValueOffset,
                LintingCodes.MalformedMetadata,
                $"'{LocalizationSchema.Marker}' carries no text."));

            return null;
        }

        var parsed = LocaleFieldParser.Parse(value);
        if (!parsed.IsValid)
        {
            diagnostics.Add(document.Diagnose(
                marker.ValueOffset,
                LintingCodes.MalformedMetadata,
                $"'{LocalizationSchema.Marker}' is malformed; expected '{Expected(expectedCulture)}'."));

            return null;
        }

        return new LocalizationFileContext(document, marker, parsed, value, expectedCulture);
    }

    private static int IndexOfMarker(LocalizationDocument document)
    {
        for (var index = 0; index < document.Entries.Count; index++)
        {
            if (document.Entries[index].IsTopLevel && string.Equals(document.Entries[index].Name, LocalizationSchema.Marker, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static string Expected(string? culture)
        => $"{LocalizationSchema.SchemaVersionField}: {LocalizationSchema.CurrentVersion}; {LocalizationSchema.CultureField}: {culture ?? "<culture>"};";
}
