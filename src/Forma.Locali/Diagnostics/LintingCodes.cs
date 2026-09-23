namespace Forma.Locali.Diagnostics;

/// <summary>Every rule the linter can report, with the severity each carries by default.</summary>
/// <remarks>The linting configuration reports under 100, a file rule in the 100s, and a callsite rule in the 200s, so the range names the family.</remarks>
public static class LintingCodes
{
    /// <summary>Every rule, ordered by the identifier it reports under: the linting configuration under 100, then files in the 100s, then callsites in the 200s.</summary>
    // Computed, not stored: a stored initialiser runs before the codes below it and would capture nulls.
    public static IReadOnlyList<LintingCode> All
        =>
        [
            MalformedConfiguration,
            StaleConfigurationGlob,
            MalformedLocaleConfig,
            RootNamespaceWithoutTypes,
            UnknownLocalePackAssembly,
            InvalidJson,
            MissingMarker,
            MarkerNotFirst,
            MalformedMetadata,
            MissingRequiredField,
            RequiredFieldOutOfOrder,
            SchemaVersionMismatch,
            UnknownMetadataField,
            CultureMismatch,
            UnknownLocalizedTextHole,
            LocalizedTextHoleShapedText,
            DuplicateKey,
            UnreachableLocalization,
            InconsistentLocalization,
            ReservedEntryOutOfOrder,
            ReservedShapedKey,
            MalformedShared,
            DuplicateAnchorFile,
            TypeNamedFolder,
            UnsuppliedPluralArgument,
            ValueCountMismatch,
            SoleArgumentExpected,
            MalformedSourceExpression,
            MissingPluralOther,
            RazorNotAnalysed,
            HoleShapedText,
            MissingLocalization,
            KeyNotAnalysed,
            BareHoleKey,
            BareExpressionKey,
            InterpolatedKey,
            ReservedShapedLookup,
            ReformattedLookup,
        ];

    /// <summary>The linting configuration is not readable JSON, does not follow its shape, or names a rule this linter does not define.</summary>
    public static LintingCode MalformedConfiguration { get; } = new(
        "LOC0020",
        LintingSeverity.Error,
        "The linting configuration is not readable JSON, does not follow its shape, or names a rule this linter does not define.");

    /// <summary>A glob in the linting configuration matched no file the run linted.</summary>
    public static LintingCode StaleConfigurationGlob { get; } = new(
        "LOC0021",
        LintingSeverity.Warning,
        "A glob in the linting configuration matched no file the run linted, so it no longer excludes or leaves anything out.");

    /// <summary>The runtime's config file is not readable JSON, names a key the runtime does not read, gives a value the runtime would refuse, is one of two for an assembly, sits anywhere but its project's root, or sits in a locale pack.</summary>
    public static LintingCode MalformedLocaleConfig { get; } = new(
        "LOC0022",
        LintingSeverity.Error,
        "The runtime's config file is not readable JSON, names a key the runtime does not read, gives a value the runtime would refuse, is one of two for an assembly, sits anywhere but its project's root, or sits in a locale pack, so it does not place the assembly's files as written.");

    /// <summary>The root namespace the runtime counts a type's name from holds none of the project's types.</summary>
    public static LintingCode RootNamespaceWithoutTypes { get; } = new(
        "LOC0023",
        LintingSeverity.Warning,
        "The root namespace the runtime counts a type's name from, the config file's or the assembly's name, holds none of the project's types, so a host or a pack laying the assembly's files out by it reaches no type.");

    /// <summary>A locale pack's folder names an assembly the solution neither builds nor references.</summary>
    public static LintingCode UnknownLocalePackAssembly { get; } = new(
        "LOC0024",
        LintingSeverity.Error,
        "A locale pack's folder names an assembly the solution neither builds nor references, so the runtime reads nothing from it and the types it was written for render in the source language.");

    /// <summary>The file is not readable JSON.</summary>
    public static LintingCode InvalidJson { get; } = new(
        "LOC0100",
        LintingSeverity.Error,
        "A file that claims to carry translations is not readable JSON.");

    /// <summary>The file carries no metadata entry.</summary>
    public static LintingCode MissingMarker { get; } = new(
        "LOC0101",
        LintingSeverity.Error,
        "A file carries no metadata entry.");

    /// <summary>The metadata entry is not the file's first property.</summary>
    public static LintingCode MarkerNotFirst { get; } = new(
        "LOC0102",
        LintingSeverity.Warning,
        "A metadata entry is not the file's first property.");

    /// <summary>The metadata entry does not follow the field grammar.</summary>
    public static LintingCode MalformedMetadata { get; } = new(
        "LOC0103",
        LintingSeverity.Error,
        "A metadata entry does not follow the field grammar.");

    /// <summary>The metadata entry omits a field the schema requires.</summary>
    public static LintingCode MissingRequiredField { get; } = new(
        "LOC0104",
        LintingSeverity.Error,
        "A metadata entry omits a field the schema requires.");

    /// <summary>The metadata entry declares its required fields out of order.</summary>
    public static LintingCode RequiredFieldOutOfOrder { get; } = new(
        "LOC0105",
        LintingSeverity.Warning,
        "A metadata entry declares its required fields out of order.");

    /// <summary>The file is written for a schema this build does not define.</summary>
    public static LintingCode SchemaVersionMismatch { get; } = new(
        "LOC0106",
        LintingSeverity.Error,
        "A file is written for a schema this build does not define.");

    /// <summary>The metadata entry declares a field the schema does not define.</summary>
    public static LintingCode UnknownMetadataField { get; } = new(
        "LOC0107",
        LintingSeverity.Warning,
        "A metadata entry declares a field the schema does not define.");

    /// <summary>The file declares a culture its own name does not give it.</summary>
    public static LintingCode CultureMismatch { get; } = new(
        "LOC0108",
        LintingSeverity.Error,
        "A file declares a culture its own name does not give it.");

    /// <summary>A translation fills a hole its key does not name.</summary>
    public static LintingCode UnknownLocalizedTextHole { get; } = new(
        "LOC0109",
        LintingSeverity.Error,
        "A translation fills a hole its key does not name, so it renders as written.");

    /// <summary>A translation writes text shaped like a hole whose name the runtime cannot read.</summary>
    public static LintingCode LocalizedTextHoleShapedText { get; } = new(
        "LOC0110",
        LintingSeverity.Error,
        "A translation writes text shaped like a hole that names nothing a value can be read by, so it renders as written.");

    /// <summary>The file declares the same key more than once.</summary>
    public static LintingCode DuplicateKey { get; } = new(
        "LOC0111",
        LintingSeverity.Error,
        "A file declares the same key more than once, so every translation but the last is dropped.");

    /// <summary>A locale file, or a section of one, names a type that does not exist.</summary>
    public static LintingCode UnreachableLocalization { get; } = new(
        "LOC0112",
        LintingSeverity.Error,
        "No type reads this file, or a section of it, by the full name its position implies, so the runtime never finds those translations.");

    /// <summary>A file translates the same key differently across its sections.</summary>
    public static LintingCode InconsistentLocalization { get; } = new(
        "LOC0113",
        LintingSeverity.Warning,
        "A file translates the same key differently across its sections, so the same English text reads two ways at run time.");

    /// <summary>A reserved entry sits after an entry it has to precede.</summary>
    public static LintingCode ReservedEntryOutOfOrder { get; } = new(
        "LOC0114",
        LintingSeverity.Warning,
        "A reserved entry sits after an entry it has to precede: the metadata entry, then the shared section lead a file.");

    /// <summary>A key or section is shaped like a reserved entry the runtime does not define.</summary>
    public static LintingCode ReservedShapedKey { get; } = new(
        "LOC0115",
        LintingSeverity.Error,
        "A key or section is shaped like a reserved entry, '$' followed by one lowercase word, but names none the runtime defines, so the file does not load.");

    /// <summary>A shared entry is not an object holding translations alone.</summary>
    public static LintingCode MalformedShared { get; } = new(
        "LOC0116",
        LintingSeverity.Error,
        "A shared entry is not an object, or nests a section, so the file does not load; a shared section holds translations alone.");

    /// <summary>Two files of one source stand for one anchor in one culture.</summary>
    public static LintingCode DuplicateAnchorFile { get; } = new(
        "LOC0117",
        LintingSeverity.Warning,
        "Two files of one source stand for one anchor in one culture, so both are read and, where they share a key, the file the later path reads wins.");

    /// <summary>A folder of locale files is named for a type.</summary>
    public static LintingCode TypeNamedFolder { get; } = new(
        "LOC0118",
        LintingSeverity.Warning,
        "A folder of locale files is named for a type, which lays the type out as a namespace; a type's own file is named for the type among its namespace folder's locale files.");

    /// <summary>An expression names an argument the lookup never supplies.</summary>
    public static LintingCode UnsuppliedPluralArgument { get; } = new(
        "LOC0200",
        LintingSeverity.Error,
        "An expression names an argument the lookup does not supply, so it counts as zero.");

    /// <summary>The lookup supplies a different number of values than its key names holes.</summary>
    public static LintingCode ValueCountMismatch { get; } = new(
        "LOC0201",
        LintingSeverity.Error,
        "A lookup supplies a different number of values than its key names holes, so it throws.");

    /// <summary>An expression naming no argument is not given the single value it reads.</summary>
    public static LintingCode SoleArgumentExpected { get; } = new(
        "LOC0202",
        LintingSeverity.Error,
        "An expression naming no argument reads a single value the lookup does not supply, so it throws.");

    /// <summary>A key written in source carries a malformed expression.</summary>
    public static LintingCode MalformedSourceExpression { get; } = new(
        "LOC0203",
        LintingSeverity.Error,
        "A key written at a callsite carries a malformed expression.");

    /// <summary>A plural expression omits the category every other one falls back to.</summary>
    public static LintingCode MissingPluralOther { get; } = new(
        "LOC0204",
        LintingSeverity.Warning,
        "A plural expression omits 'other', so a count it does not name renders as nothing.");

    /// <summary>A project's Razor markup was not checked, because no generated C# was found.</summary>
    public static LintingCode RazorNotAnalysed { get; } = new(
        "LOC0205",
        LintingSeverity.Warning,
        "A project's Razor markup went unchecked because no generated C# was found.");

    /// <summary>A key writes text shaped like a hole whose name the runtime cannot read.</summary>
    public static LintingCode HoleShapedText { get; } = new(
        "LOC0206",
        LintingSeverity.Error,
        "A key writes text shaped like a hole that names nothing a value can be read by, so it renders as written.");

    /// <summary>A key written in source is not answered in a culture its type is already translated for.</summary>
    public static LintingCode MissingLocalization { get; } = new(
        "LOC0207",
        LintingSeverity.Warning,
        "A key written at a callsite has no entry in a culture its type is already translated for, so it renders untranslated.");

    /// <summary>A lookup writes its key as an expression, so no rule could read it.</summary>
    public static LintingCode KeyNotAnalysed { get; } = new(
        "LOC0208",
        LintingSeverity.Warning,
        "A lookup writes its key as an expression, so what it reads went unchecked.");

    /// <summary>The key carries holes but no words for a translator to read.</summary>
    public static LintingCode BareHoleKey { get; } = new(
        "LOC0209",
        LintingSeverity.Warning,
        "A key carries holes but no words anywhere else, so it names no text to translate.");

    /// <summary>The expression key carries no words in any field, only the count.</summary>
    public static LintingCode BareExpressionKey { get; } = new(
        "LOC0210",
        LintingSeverity.Warning,
        "An expression key carries no words in any field, so it names no text to translate.");

    /// <summary>A lookup writes its key as an interpolated string with holes.</summary>
    public static LintingCode InterpolatedKey { get; } = new(
        "LOC0211",
        LintingSeverity.Error,
        "A lookup writes its key as an interpolated string with holes, so the formatted text becomes the key and no translation matches it.");

    /// <summary>A lookup writes a key shaped like a reserved entry.</summary>
    public static LintingCode ReservedShapedLookup { get; } = new(
        "LOC0212",
        LintingSeverity.Error,
        "A lookup writes a key shaped like a reserved entry, '$' followed by one lowercase word, which no file can answer.");

    /// <summary>A lookup's already-substituted result supplies string.Format's format text.</summary>
    public static LintingCode ReformattedLookup { get; } = new(
        "LOC0213",
        LintingSeverity.Error,
        "A lookup supplies string.Format's format text, which reads its own '{0}' rather than the runtime's holes, and throws once a translation names one.");
}
