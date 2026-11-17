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
            StaleConfigurationEntry,
            MalformedLocaleConfig,
            RootNamespaceWithoutTypes,
            UnknownLocalePackAssembly,
            DisallowedLocaleSource,
            UnreadConfiguration,
            CultureInRootNamespace,
            InvalidHint,
            ConflictingHint,
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
            TypeNamedFolder,
            MissingCultureCoverage,
            EntryConflict,
            AmbiguousName,
            CultureInAnotherCase,
            SecondCultureInName,
            CultureOutsideSatellite,
            UnsuppliedPluralArgument,
            ValueCountMismatch,
            SoleArgumentExpected,
            MalformedSourceExpression,
            MissingPluralOther,
            RazorNotAnalysed,
            HoleShapedText,
            MissingLocalization,
            BareHoleKey,
            BareExpressionKey,
            InterpolatedKey,
            ReservedShapedLookup,
            ReformattedLookup,
            NotRestored,
            UnlocalizedType,
        ];

    /// <summary>The identifiers of the rules this linter has retired, which a linting configuration may still name and no run reports.</summary>
    public static IReadOnlySet<string> Retired { get; } = new HashSet<string>(StringComparer.Ordinal) { "LOC0117", "LOC0208", "LOC0214", "LOC0215" };

    /// <summary>The linting configuration is not readable JSON, does not follow its shape, or names a rule this linter does not define.</summary>
    public static LintingCode MalformedConfiguration { get; } = new(
        "LOC0020",
        LintingSeverity.Error,
        "The linting configuration is not readable JSON, does not follow its shape, or names a rule this linter does not define.");

    /// <summary>An entry in the linting configuration applied to no file the run reached, or names a rule this linter has retired.</summary>
    public static LintingCode StaleConfigurationEntry { get; } = new(
        "LOC0021",
        LintingSeverity.Warning,
        "An entry in the linting configuration applied to no file the run reached, or names a rule this linter has retired, so it excludes, leaves out, or governs nothing.");

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

    /// <summary>A locale pack's file has a name starting with no assembly the solution builds or references.</summary>
    public static LintingCode UnknownLocalePackAssembly { get; } = new(
        "LOC0024",
        LintingSeverity.Error,
        "A locale pack's file has a name starting with no assembly the solution builds or references, so the runtime reads nothing from it and the types it was written for render in the source language.");

    /// <summary>A locale file comes from a source the linting configuration in force over it does not allow.</summary>
    public static LintingCode DisallowedLocaleSource { get; } = new(
        "LOC0025",
        LintingSeverity.Warning,
        "A locale file comes from a source the linting configuration in force over it does not allow, embedded, satellite, pack or host; the runtime reads it all the same, so entries are kept in a place the repository said they are not.");

    /// <summary>A <c>.locali.json</c> the run does not read sits beneath the path it was given.</summary>
    public static LintingCode UnreadConfiguration { get; } = new(
        "LOC0026",
        LintingSeverity.Warning,
        "A .locali.json sits beneath the path the run was given while the run reads only the one nearest above that path, so nothing in it applies.");

    /// <summary>A part of the root namespace a project's locale files are embedded under, the assembly's name unless the project names another, is a culture.</summary>
    public static LintingCode CultureInRootNamespace { get; } = new(
        "LOC0027",
        LintingSeverity.Warning,
        "A part of the root namespace a project's locale files are embedded under is a culture, which the runtime reads as the culture of every file embedded under it.");

    /// <summary>A hint names a type, a member or a package that is not there or not of the kind the hint reads.</summary>
    public static LintingCode InvalidHint { get; } = new(
        "LOC0028",
        LintingSeverity.Error,
        "A hint names a type, a member or a package that is not there or not of the kind the hint reads, so it relates nothing and the lookups it was written for go unchecked.");

    /// <summary>A hint disagrees with another written for the same member, or with the type the flow reaches on its own.</summary>
    public static LintingCode ConflictingHint { get; } = new(
        "LOC0029",
        LintingSeverity.Error,
        "A hint disagrees with another written for the same member, or with the type the flow reaches on its own, so it is set aside, and a run that cannot follow the flow, as one over the compiled library, would hold lookups to the wrong type.");

    /// <summary>The file is not readable JSON.</summary>
    public static LintingCode InvalidJson { get; } = new(
        "LOC0100",
        LintingSeverity.Error,
        "A file that claims to be a locale file is not readable JSON.");

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

    /// <summary>A text fills a hole its key does not name.</summary>
    public static LintingCode UnknownLocalizedTextHole { get; } = new(
        "LOC0109",
        LintingSeverity.Error,
        "A text fills a hole its key does not name, so it renders as written.");

    /// <summary>A text writes something shaped like a hole whose name the runtime cannot read.</summary>
    public static LintingCode LocalizedTextHoleShapedText { get; } = new(
        "LOC0110",
        LintingSeverity.Error,
        "A text writes something shaped like a hole that names nothing a value can be read by, so it renders as written.");

    /// <summary>The file declares the same key more than once.</summary>
    public static LintingCode DuplicateKey { get; } = new(
        "LOC0111",
        LintingSeverity.Error,
        "A file declares the same key more than once, so every text but the last is dropped.");

    /// <summary>A locale file, or a section of one, names a type that does not exist.</summary>
    public static LintingCode UnreachableLocalization { get; } = new(
        "LOC0112",
        LintingSeverity.Error,
        "No type reads this file, or a section of it, by the full name its position implies, so the runtime never finds those entries.");

    /// <summary>A file gives the same key different texts across its sections.</summary>
    public static LintingCode InconsistentLocalization { get; } = new(
        "LOC0113",
        LintingSeverity.Warning,
        "A file gives the same key different texts across its sections, so the same source text reads two ways at run time.");

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

    /// <summary>A shared entry is not an object holding entries alone.</summary>
    public static LintingCode MalformedShared { get; } = new(
        "LOC0116",
        LintingSeverity.Error,
        "A shared entry is not an object, or nests a section, so the file does not load; a shared section holds entries alone.");

    /// <summary>A folder of locale files is named for a type.</summary>
    public static LintingCode TypeNamedFolder { get; } = new(
        "LOC0118",
        LintingSeverity.Warning,
        "A folder of locale files is named for a type, which lays the type out as a namespace; a type's own file is named for the type among its namespace folder's locale files.");

    /// <summary>A type is localized in one culture family the project's files or packs carry and left out of another.</summary>
    public static LintingCode MissingCultureCoverage { get; } = new(
        "LOC0119",
        LintingSeverity.Warning,
        "A type is localized in one culture family the project's files or packs carry and left out of another, so it renders in the source language for every reader of the missing one.");

    /// <summary>Two files of one source give an entry a type reads different texts.</summary>
    public static LintingCode EntryConflict { get; } = new(
        "LOC0120",
        LintingSeverity.Error,
        "Two files of one source give an entry a type reads different texts, so the runtime keeps the text of the file whose name sorts last and logs the conflict.");

    /// <summary>A section of an application's file is read both for the application's own types and for another assembly's, a type of each reaching it.</summary>
    public static LintingCode AmbiguousName { get; } = new(
        "LOC0121",
        LintingSeverity.Warning,
        "An application's file has a name that reads both in the application's own namespace and as another assembly's, and a type of each reads one of its sections, so its entries answer both.");

    /// <summary>A part of a locale file's name is a culture written in another case than .NET names it, and names no type.</summary>
    public static LintingCode CultureInAnotherCase { get; } = new(
        "LOC0122",
        LintingSeverity.Warning,
        "A part of a locale file's name is a culture written in another case than .NET names it, and names no type, so the runtime reads it as part of the name rather than as the file's culture.");

    /// <summary>A part of a locale file's name is a culture other than the file's own, and names no type.</summary>
    public static LintingCode SecondCultureInName { get; } = new(
        "LOC0123",
        LintingSeverity.Warning,
        "A part of a locale file's name is a culture other than the one the name gives the file, and names no type, so the runtime reads it as part of the name the file stands for, which no type reads.");

    /// <summary>A satellite assembly holds a locale file under a name whose culture is not the satellite's.</summary>
    public static LintingCode CultureOutsideSatellite { get; } = new(
        "LOC0124",
        LintingSeverity.Error,
        "A satellite assembly holds a locale file under a name whose culture is not the satellite's, so the runtime reads none of its entries.");

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

    /// <summary>A key written in source is not answered in a culture its type is already localized in.</summary>
    public static LintingCode MissingLocalization { get; } = new(
        "LOC0207",
        LintingSeverity.Warning,
        "A key written at a callsite has no entry in a culture its type is already localized in, so it renders in the source language.");

    /// <summary>The key carries holes but no words for anyone localizing it to read.</summary>
    public static LintingCode BareHoleKey { get; } = new(
        "LOC0209",
        LintingSeverity.Warning,
        "A key carries holes but no words anywhere else, so it names no text to localize.");

    /// <summary>The expression key carries no words in any field, only the count.</summary>
    public static LintingCode BareExpressionKey { get; } = new(
        "LOC0210",
        LintingSeverity.Warning,
        "An expression key carries no words in any field, so it names no text to localize.");

    /// <summary>A lookup writes its key as an interpolated string with holes.</summary>
    public static LintingCode InterpolatedKey { get; } = new(
        "LOC0211",
        LintingSeverity.Error,
        "A lookup writes its key as an interpolated string with holes, so the formatted text becomes the key and no entry matches it.");

    /// <summary>A lookup writes a key shaped like a reserved entry.</summary>
    public static LintingCode ReservedShapedLookup { get; } = new(
        "LOC0212",
        LintingSeverity.Error,
        "A lookup writes a key shaped like a reserved entry, '$' followed by one lowercase word, which no file can answer.");

    /// <summary>A lookup's already-substituted result supplies string.Format's format text.</summary>
    public static LintingCode ReformattedLookup { get; } = new(
        "LOC0213",
        LintingSeverity.Error,
        "A lookup supplies string.Format's format text, which reads its own '{0}' rather than the runtime's holes, and throws once a text names one.");

    /// <summary>A project referencing packages was loaded without its restore output.</summary>
    public static LintingCode NotRestored { get; } = new(
        "LOC0216",
        LintingSeverity.Error,
        "A project referencing packages was loaded without its restore output, so the types its packages declare went unread and what the rules read through them went unchecked.");

    /// <summary>A type lookups read has no entries of its own in any culture its assembly's locale files carry.</summary>
    public static LintingCode UnlocalizedType { get; } = new(
        "LOC0217",
        LintingSeverity.Warning,
        "A type lookups read has no entries of its own in any culture its assembly's locale files carry, so every key read for it renders in the source language in each culture no other entry reaches it in.");
}
