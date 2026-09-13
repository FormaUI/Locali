namespace Forma.Locali.Diagnostics;

/// <summary>Every rule the linter can report, with the severity each carries by default.</summary>
/// <remarks>A file rule reports in the 100s, a call-site rule in the 200s, so the range names the family.</remarks>
public static class LintCodes
{
    /// <summary>Every rule, ordered by the identifier it reports under: files in the 100s, then call sites in the 200s.</summary>
    // Computed, not stored: a stored initialiser runs before the codes below it and would capture nulls.
    public static IReadOnlyList<LintCode> All
        =>
        [
            InvalidJson,
            MissingMarker,
            MarkerNotFirst,
            MalformedMetadata,
            MissingRequiredField,
            RequiredFieldOutOfOrder,
            SchemaVersionMismatch,
            UnknownMetadataField,
            CultureMismatch,
            UnknownTranslationHole,
            TranslationHoleShapedText,
            DuplicateKey,
            UnreachableTranslation,
            InconsistentTranslation,
            MalformedTools,
            ReservedEntryOutOfOrder,
            ReservedShapedKey,
            MalformedShared,
            UnsuppliedPluralArgument,
            ValueCountMismatch,
            SoleArgumentExpected,
            MalformedSourceExpression,
            MissingPluralOther,
            RazorNotAnalysed,
            HoleShapedText,
            MissingTranslation,
            KeyNotAnalysed,
            BareHoleKey,
            BareExpressionKey,
            InterpolatedKey,
            ReservedShapedLookup,
        ];

    /// <summary>The key carries holes but no words for a translator to read.</summary>
    public static LintCode BareHoleKey { get; } = new(
        "LOC0211",
        LintSeverity.Warning,
        "A key carries holes but no words anywhere else, so it names no text to translate.");

    /// <summary>The expression key carries no words in any field, only the count.</summary>
    public static LintCode BareExpressionKey { get; } = new(
        "LOC0212",
        LintSeverity.Warning,
        "An expression key carries no words in any field, so it names no text to translate.");

    /// <summary>The file declares a culture its own name does not give it.</summary>
    public static LintCode CultureMismatch { get; } = new(
        "LOC0108",
        LintSeverity.Error,
        "A file declares a culture its own name does not give it.");

    /// <summary>The file declares the same key more than once.</summary>
    public static LintCode DuplicateKey { get; } = new(
        "LOC0111",
        LintSeverity.Error,
        "A file declares the same key more than once, so every translation but the last is dropped.");

    /// <summary>A key writes text shaped like a hole whose name the runtime cannot read.</summary>
    public static LintCode HoleShapedText { get; } = new(
        "LOC0208",
        LintSeverity.Error,
        "A key writes text shaped like a hole that names nothing a value can be read by, so it renders as written.");

    /// <summary>A file translates the same key differently across its sections.</summary>
    public static LintCode InconsistentTranslation { get; } = new(
        "LOC0113",
        LintSeverity.Warning,
        "A file translates the same key differently across its sections, so the same English text reads two ways at run time.");

    /// <summary>A lookup writes its key as an interpolated string with holes.</summary>
    public static LintCode InterpolatedKey { get; } = new(
        "LOC0213",
        LintSeverity.Error,
        "A lookup writes its key as an interpolated string with holes, so the formatted text becomes the key and no translation matches it.");

    /// <summary>The file is not readable JSON.</summary>
    public static LintCode InvalidJson { get; } = new(
        "LOC0100",
        LintSeverity.Error,
        "A file that claims to carry translations is not readable JSON.");

    /// <summary>A lookup writes its key as an expression, so no rule could read it.</summary>
    public static LintCode KeyNotAnalysed { get; } = new(
        "LOC0210",
        LintSeverity.Warning,
        "A lookup writes its key as an expression, so what it reads went unchecked.");

    /// <summary>The metadata entry does not follow the field grammar.</summary>
    public static LintCode MalformedMetadata { get; } = new(
        "LOC0103",
        LintSeverity.Error,
        "A metadata entry does not follow the field grammar.");

    /// <summary>A key written in source carries a malformed expression.</summary>
    public static LintCode MalformedSourceExpression { get; } = new(
        "LOC0204",
        LintSeverity.Error,
        "A key written at a call site carries a malformed expression.");

    /// <summary>The metadata entry is not the file's first property.</summary>
    public static LintCode MarkerNotFirst { get; } = new(
        "LOC0102",
        LintSeverity.Warning,
        "A metadata entry is not the file's first property.");

    /// <summary>The file carries no metadata entry.</summary>
    public static LintCode MissingMarker { get; } = new(
        "LOC0101",
        LintSeverity.Error,
        "A file carries no metadata entry.");

    /// <summary>A plural expression omits the category every other one falls back to.</summary>
    public static LintCode MissingPluralOther { get; } = new(
        "LOC0205",
        LintSeverity.Warning,
        "A plural expression omits 'other', so a count it does not name renders as nothing.");

    /// <summary>The metadata entry omits a field the schema requires.</summary>
    public static LintCode MissingRequiredField { get; } = new(
        "LOC0104",
        LintSeverity.Error,
        "A metadata entry omits a field the schema requires.");

    /// <summary>A key written in source is not answered in a culture its type is already translated for.</summary>
    public static LintCode MissingTranslation { get; } = new(
        "LOC0209",
        LintSeverity.Warning,
        "A key written at a call site has no entry in a culture its type is already translated for, so it renders untranslated.");

    /// <summary>A project's Razor markup was not checked, because no generated C# was found.</summary>
    public static LintCode RazorNotAnalysed { get; } = new(
        "LOC0206",
        LintSeverity.Warning,
        "A project's Razor markup went unchecked because no generated C# was found.");

    /// <summary>The metadata entry declares its required fields out of order.</summary>
    public static LintCode RequiredFieldOutOfOrder { get; } = new(
        "LOC0105",
        LintSeverity.Warning,
        "A metadata entry declares its required fields out of order.");

    /// <summary>The file is written for a schema this build does not define.</summary>
    public static LintCode SchemaVersionMismatch { get; } = new(
        "LOC0106",
        LintSeverity.Error,
        "A file is written for a schema this build does not define.");

    /// <summary>An expression naming no argument is not given the single value it reads.</summary>
    public static LintCode SoleArgumentExpected { get; } = new(
        "LOC0203",
        LintSeverity.Error,
        "An expression naming no argument reads a single value the lookup does not supply, so it throws.");

    /// <summary>A translation writes text shaped like a hole whose name the runtime cannot read.</summary>
    public static LintCode TranslationHoleShapedText { get; } = new(
        "LOC0110",
        LintSeverity.Error,
        "A translation writes text shaped like a hole that names nothing a value can be read by, so it renders as written.");

    /// <summary>The metadata entry declares a field the schema does not define.</summary>
    public static LintCode UnknownMetadataField { get; } = new(
        "LOC0107",
        LintSeverity.Warning,
        "A metadata entry declares a field the schema does not define.");

    /// <summary>A translation fills a hole its key does not name.</summary>
    public static LintCode UnknownTranslationHole { get; } = new(
        "LOC0109",
        LintSeverity.Error,
        "A translation fills a hole its key does not name, so it renders as written.");

    /// <summary>The tools entry is misplaced or malformed, or what it holds for this linter names something this linter does not define.</summary>
    public static LintCode MalformedTools { get; } = new(
        "LOC0114",
        LintSeverity.Error,
        "The tools entry is misplaced or malformed, or what it holds for this linter names something this linter does not define, so nothing is left out.");

    /// <summary>A shared entry is not an object holding translations alone.</summary>
    public static LintCode MalformedShared { get; } = new(
        "LOC0117",
        LintSeverity.Error,
        "A shared entry is not an object, or nests a section, so the file does not load; a shared section holds translations alone.");

    /// <summary>A reserved entry sits after an entry it has to precede.</summary>
    public static LintCode ReservedEntryOutOfOrder { get; } = new(
        "LOC0115",
        LintSeverity.Warning,
        "A reserved entry sits after an entry it has to precede: the metadata entry, then the tools entry, then the shared section lead a file.");

    /// <summary>A key or section is shaped like a reserved entry the runtime does not define.</summary>
    public static LintCode ReservedShapedKey { get; } = new(
        "LOC0116",
        LintSeverity.Error,
        "A key or section is shaped like a reserved entry, '$' followed by one lowercase word, but names none the runtime defines, so the file does not load.");

    /// <summary>A lookup writes a key shaped like a reserved entry.</summary>
    public static LintCode ReservedShapedLookup { get; } = new(
        "LOC0214",
        LintSeverity.Error,
        "A lookup writes a key shaped like a reserved entry, '$' followed by one lowercase word, which no file can answer.");

    /// <summary>A translation file, or a section of one, names a type that does not exist.</summary>
    public static LintCode UnreachableTranslation { get; } = new(
        "LOC0112",
        LintSeverity.Error,
        "No type reads this file, or a section of it, by the full name its position implies, so the runtime never finds those translations.");

    /// <summary>An expression names an argument the lookup never supplies.</summary>
    public static LintCode UnsuppliedPluralArgument { get; } = new(
        "LOC0201",
        LintSeverity.Error,
        "An expression names an argument the lookup does not supply, so it counts as zero.");

    /// <summary>The lookup supplies a different number of values than its key names holes.</summary>
    public static LintCode ValueCountMismatch { get; } = new(
        "LOC0202",
        LintSeverity.Error,
        "A lookup supplies a different number of values than its key names holes, so it throws.");
}
