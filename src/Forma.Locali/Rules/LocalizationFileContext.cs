namespace Forma.Locali.Rules;

using Forma.Framework.Localization.Parser.Syntax;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;

/// <summary>What a file rule reads, and what it reports through.</summary>
/// <param name="document">The file being checked.</param>
/// <param name="marker">The metadata entry, already found.</param>
/// <param name="metadata">The metadata fields, already parsed.</param>
/// <param name="metadataText">The metadata entry's text, which the parsed spans point into.</param>
/// <param name="expectedCulture">The culture the file's own name gives it, or null when its name carries none.</param>
public sealed class LocalizationFileContext(
    LocalizationDocument document,
    LocalizationEntry marker,
    LocaleFieldResult metadata,
    string metadataText,
    string? expectedCulture)
{
    private readonly List<LintDiagnostic> _reported = [];

    /// <summary>The file being checked.</summary>
    public LocalizationDocument Document { get; } = document ?? throw new ArgumentNullException(nameof(document));

    /// <summary>The metadata entry every rule reports against.</summary>
    public LocalizationEntry Marker { get; } = marker;

    /// <summary>The metadata fields, parsed once for every rule.</summary>
    public LocaleFieldResult Metadata { get; } = metadata;

    /// <summary>The metadata entry's text, which the parsed spans point into.</summary>
    public string MetadataText { get; } = metadataText ?? throw new ArgumentNullException(nameof(metadataText));

    /// <summary>The culture the file's own name gives it, or null when its name carries none.</summary>
    public string? ExpectedCulture { get; } = expectedCulture;

    /// <summary>Everything the rules reported.</summary>
    public IReadOnlyList<LintDiagnostic> Reported
        => _reported;

    /// <summary>The value a metadata field carries, or null when it declares none.</summary>
    /// <param name="name">The field to read.</param>
    public string? Field(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        foreach (var field in Metadata.Fields)
        {
            if (field.NameSpan.Slice(MetadataText).SequenceEqual(name))
            {
                return field.ValueSpan.Slice(MetadataText).ToString();
            }
        }

        return null;
    }

    /// <summary>The name of every field the metadata declares, in the order it declares them.</summary>
    public IReadOnlyList<string> FieldNames()
        => [.. Metadata.Fields.Select(field => field.NameSpan.Slice(MetadataText).ToString())];

    /// <summary>Reports against the metadata entry.</summary>
    /// <param name="code">The rule reporting.</param>
    /// <param name="message">What is wrong, in a sentence.</param>
    public void Report(LintCode code, string message)
        => Report(code, Marker.ValueOffset, message);

    /// <summary>Reports against a byte offset in the file.</summary>
    /// <param name="code">The rule reporting.</param>
    /// <param name="offset">Where in the file the diagnostic belongs.</param>
    /// <param name="message">What is wrong, in a sentence.</param>
    public void Report(LintCode code, int offset, string message)
    {
        ArgumentNullException.ThrowIfNull(code);

        _reported.Add(Document.Diagnose(offset, code, message, code.DefaultSeverity));
    }

    /// <summary>Reports against a byte offset, overriding the rule's usual severity.</summary>
    /// <param name="code">The rule reporting.</param>
    /// <param name="offset">Where in the file the diagnostic belongs.</param>
    /// <param name="message">What is wrong, in a sentence.</param>
    /// <param name="severity">How much this particular finding matters.</param>
    public void Report(LintCode code, int offset, string message, LintSeverity severity)
    {
        ArgumentNullException.ThrowIfNull(code);

        _reported.Add(Document.Diagnose(offset, code, message, severity));
    }
}
