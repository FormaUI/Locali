# Rules

Every rule `locali` reports, one entry each. An entry says what the rule reports, why it
matters at run time, what fixes it, and where it deliberately stays silent. The linting
configuration reports under 100, a file rule in the 100s, and a callsite rule in the 200s, so
the range names the family.

An **error** is something the runtime will get wrong; a **warning** is something worth changing
that still loads. A default run reports errors; `--severity warning` reports both.

## Linting configuration (LOC0020–LOC0099)

Two files configure two things. `.locali.json` is the linter's own: it cascades like an
`.editorconfig`, every one found walking up from a file applying together, the nearer adding to
the farther, and the runtime never reads it. `.locale.json` is the runtime's: one optional file
per assembly, at its project's root, cascading nowhere, which the linter reads through the
runtime's own reader and never writes a key of its own into.

```json
{
  "ignoreDiagnostics": {
    "**/Forma.Blazor.Showcase/**/LocalizationPage.razor": ["LOC0207"]
  },
  "excludeFiles": ["*/Fixtures/*"]
}
```

`ignoreDiagnostics` pairs a glob with the rules a file it matches is left out of, and is the one
place a per-file exception is written, since a locale file holds translations and nothing a tool
reads. `excludeFiles` mirrors `--exclude`, so a fixture folder is excluded once rather than on
every invocation. Globs are case-insensitive, written with forward slashes, `*` and `**` both
spanning separators, and read relative to the folder the file sits in. Comments and trailing
commas are allowed, as in a locale file. Leaving a diagnostic out is never silent: it is counted
in `suppressedCount` and named in the console brief.

### LOC0020 — The linting configuration cannot be read

**Reports.** A `.locali.json` that is not readable JSON, does not follow its shape, or names a
rule this linter does not define, in either list.

**Why it matters.** A typo in a rule identifier would silence nothing and say nothing; the file
is refused instead. The rule over `.locali.json` itself can never be named in its own
`ignoreDiagnostics` list.

**What fixes it.** Correct the file; the message names the key or the identifier it refused.

### LOC0021 — A glob matched nothing

**Reports.** A glob in `ignoreDiagnostics` or `excludeFiles` that matched no file the run
reached.

**Why it matters.** A file moved out of the tree is exactly when its exceptions go stale, and an
exception that names nothing sits unnoticed until it excuses the wrong file.

**What fixes it.** Delete the entry, or point it at the file's new place.

**Silent where.** The run did not reach the folder the glob names: a narrower `--path` leaves
globs for the rest of the tree unjudged. It is a warning for that reason.

### LOC0022 — The runtime's config file is wrong or misplaced

**Reports.** A `.locale.json` that is not readable JSON, names a key the runtime does not read,
gives a value the runtime would refuse, is one of two embedded for an assembly, is embedded
anywhere but the project's root, or sits in a host's `.locales` folder.

**Why it matters.** The runtime passes over a key it does not read, so a misspelled one steers
nothing; it refuses every lookup in an assembly that embeds two config files; it reads a file
embedded beneath the root as the assembly's all the same, so the one place it may sit is stated
at lint time; and it reads no config from a host's folder at all.

**What fixes it.** Keep the keys to `rootNamespace`, `rootDir`, `localesDir` and `paths`, each
as the runtime spells it; a malformed path is reported at the token that is wrong. Embed one
file, at the project's root, and lay none in a host's folder.

**Silent where.** The file reads cleanly and sits where it belongs. The type-bound rules then
read the project's files through it, as the runtime does.

### LOC0023 — The root namespace holds none of the project's types

**Reports.** The root namespace the runtime counts a type's name from, `rootNamespace` in the
project's `.locale.json` or the assembly's name where none is declared, when no type of the
project is under it. A declared one is reported at the config file; an undeclared one at the
project, and only where the project embeds locale files or a locale pack places a file for it
under the root and nowhere else.

**Why it matters.** A project's `RootNamespace` and its config's `rootNamespace` drift apart when
one is renamed without the other, or when a project whose root namespace is not its assembly's
name never declares it. The runtime then reads every type by its full name, so the embedded
files still load, but a host's folder or a locale pack laying the assembly's files out by the
root namespace reaches no type, without a word.

**What fixes it.** Declare the namespace the project's types share as `rootNamespace`; the
report names the project's own `RootNamespace` where the types sit under it.

**Silent where.** A type of the project is under the root namespace, whatever the project's
`RootNamespace` says, since the runtime never reads that; the project declares no types, or did
not compile; the project's items could not be read, so no config file is known to be embedded;
a locale pack's folder for the project spells full type names rather than the root, which the
runtime reads for a type outside it; or the config file will not read, which is LOC0022's.

### LOC0024 — A pack folder names an assembly the solution does not have

**Reports.** A top-level folder of a locale pack, a project declaring `IsLocalePack`, whose name
is no assembly a project of the solution builds or references. It is reported at the pack's
project file, naming the folder.

**Why it matters.** A pack's folders are the only thing that says which assemblies it localizes,
and the runtime matches a folder to an assembly by name and nothing else. A misspelled folder
loads with the pack, answers no lookup, and every type it was written for renders in the source
language without a word.

**What fixes it.** Name the folder for the assembly exactly, `Forma.Blazor.Composer` rather than
the namespace its types share.

**Silent where.** The run named a project or a directory rather than a solution, since only a
solution lists every project a folder could name; a project of the solution did not compile, or
is written in a language the linter does not read, so its assembly's name cannot be told; the
folder names an assembly a project references from a package, whose types are out of reach; or
the pack's items could not be read.

## Locale files (LOC0100–LOC0199)

A locale file opens with `$localization`, then `$shared` where it carries one, then its
translations and sections.

### LOC0100 — Not readable JSON

**Reports.** A file that claims to carry translations but is not readable JSON.

**Why it matters.** The runtime refuses the file, and every type it would have localized falls
back to English.

**What fixes it.** Make it JSON. A `//` or `/* */` comment and a trailing comma are accepted,
as the runtime's own reader accepts them.

### LOC0101 — No metadata entry

**Reports.** A file carrying no `$localization` entry.

**Why it matters.** The entry is what marks a file as a locale file; without it the runtime
refuses the file.

**What fixes it.** Open the file with `"$localization": "SchemaVersion: 1.0.0; Culture: he;"`.

### LOC0102 — The metadata entry is not first

**Reports.** A `$localization` entry that is not the file's first property.

**Why it matters.** The file loads, but the entry that says what the file is sits where a reader
looks last.

**What fixes it.** Move it to the top.

### LOC0103 — Malformed metadata

**Reports.** A `$localization` value that does not follow the `Name: value;` field grammar.

**Why it matters.** The runtime cannot read the fields, so it refuses the file.

**What fixes it.** Write fields as `Name: value;` pairs, the trailing `;` included.

### LOC0104 — A required field is missing

**Reports.** A `$localization` entry omitting `SchemaVersion` or `Culture`.

**Why it matters.** The runtime needs both to read the file, and refuses it without them.

**What fixes it.** Declare both.

### LOC0105 — Required fields out of order

**Reports.** `Culture` declared before `SchemaVersion`.

**Why it matters.** The file loads; the order is the schema's convention, kept for a reader.

**What fixes it.** `SchemaVersion` first, then `Culture`, then any optional field.

### LOC0106 — A schema this build does not define

**Reports.** A `SchemaVersion` whose major differs from this build's.

**Why it matters.** The runtime refuses a differing major. A differing minor is accepted in
either direction, and patch is ignored.

**What fixes it.** Write the file for the schema this build defines.

### LOC0107 — An unknown metadata field

**Reports.** A `$localization` field the schema does not define.

**Why it matters.** The runtime ignores it, so a misspelled `Author` records nothing; a field a
newer minor schema defines is the one case the warning excuses on purpose.

**What fixes it.** Spell the field as the schema does, or drop it.

### LOC0108 — The culture disagrees with the file's name

**Reports.** A `Culture` other than the one the file's own name gives it.

**Why it matters.** The runtime refuses the file: the name decides which culture the file is
read for, and the entry has to agree.

**What fixes it.** Make the entry say what the name says.

### LOC0109 — A translation fills a hole its key does not name

**Reports.** A translation writing `{stord}` for a key that names `{stored}`.

**Why it matters.** The runtime renders `{stord}` literally, in front of a user, and nothing else
complains.

**What fixes it.** Name the holes the key names. A name may be written as a brace hole or as a
plural expression's bracketed argument, the two are equivalent, and a translation may reorder or
drop a name freely.

### LOC0110 — A translation writes text shaped like a hole the runtime cannot read

**Reports.** `{0}`, `{a.b}` or `{stored,x}` in a translation: a shape that could only have been
meant as a hole, whose name the runtime cannot read or whose alignment is not a number.

**Why it matters.** The text renders as written.

**What fixes it.** Write the hole as `{Name}`, optionally `{Name,alignment}` or
`{Name:specifier}`.

**Silent where.** The text contains a space, as `{ color: red }` or `{colour, red}` does: that
is prose, rendered as written and meant to be. LOC0206 is the same rule at a callsite.

### LOC0111 — A duplicate key

**Reports.** A key declared more than once in one section.

**Why it matters.** Every translation but the last is dropped, silently.

**What fixes it.** Keep one. A key repeating across sections is two types sharing a string, which
LOC0113 reads instead.

### LOC0112 — A file or section names a type that does not exist

**Reports.** A file no type reads, a section naming no type, or the entries outside any section
in a file read for its sections alone; and a file no path in force places. Every report names
the anchors the runtime places the file at.

**Why it matters.** The runtime resolves translations by the owning type's full name, from a
file's position and the sections inside it, so a moved folder, a renamed type or a misspelled
section strands its translations while every other check stays green.

**What fixes it.** Put the file where the type's name says, or name the type in a section: from
the file's position, or from the assembly's root with a leading `@.`. The rule reads through the
runtime's own source, from the root down to the type, for every type the project declares and
every culture its files carry, under the name the build gives each file, an explicit
`LogicalName` included, and through the `.locale.json` the project embeds. A locale pack's
folder for the project is read the same way, laid out by the project's root namespace, so a
pack's file is reported where it sits in the pack.

**Silent where.** The run did not name the project's files; the file is one the project does not
embed — a host's folder, which translates other assemblies' types; the file is a locale pack's
whose folder names no project of the run, which LOC0024 reports; or the project's items could
not be read and the file sits under a folder the build renames to embed it, such as `my-folder`,
so its name cannot be told.

### LOC0113 — A key translated two ways across sections

**Reports.** Sections of one file that translate the same key differently, each after the first
naming the section it differs from. A `null` reads as the key text, and the last declaration in
a section is the one compared.

**Why it matters.** Both load, so the same English text reads two ways across the application.

**What fixes it.** Agree the translations, or say in `.locali.json` that they differ on purpose,
where a translation agrees in gender or number with the thing its type describes. That case is
why the rule is a warning.

### LOC0114 — A reserved entry out of order

**Reports.** A `$shared` before `$localization`, or either after a translation or a section.

**Why it matters.** The file loads; a file leads with what it is and what it shares.

**What fixes it.** `$localization`, then `$shared`, then the rest.

### LOC0115 — A key shaped like a reserved entry the runtime does not define

**Reports.** A key or section, at any level, that is `$` followed by one lowercase word and
nothing else — `$import`, `$shraed` — which the runtime does not define.

**Why it matters.** The runtime refuses the file rather than read the key as text.

**What fixes it.** Spell the reserved entry as defined, or escape a key meant as text with a
doubled sign: `$$price` is the text `$price`.

**Silent where.** Anything else starting with `$` is prose: `$`, `$ per month`, `$5 off`,
`$USD`, `$camelCase`, `$price/month`, because an uppercase letter, a digit, a dot or a space
breaks the shape. LOC0212 is the same rule at a callsite.

### LOC0116 — A malformed `$shared` entry

**Reports.** A `$shared` that is not an object, or that nests a section or another `$shared`.

**Why it matters.** The runtime refuses the file. `$shared` holds translations alone, which every
type reached through its node gets unless a section of its own says otherwise.

**What fixes it.** Keep translations only in it; put a nested section beside it.

### LOC0117 — Two files stand for one anchor

**Reports.** Two files of one source standing for one anchor in one culture, through different
paths or through a host's two forms of a file. Each file is reported, naming the other.

**Why it matters.** The runtime reads both, in the order of the paths that place them, so where
they share a key the later wins without a word, and a file meant to be moved rather than copied
keeps answering from where it was left.

**What fixes it.** Keep one. The rule reads a project's embedded files as one source, the
host's `.locales` folder at the project's root as another, and each locale pack's folder for the
project as another, in which a file may sit in a folder's own `.locales` or directly in the
folder. It is a warning because both files load.

### LOC0118 — A folder named for a type

**Reports.** A file named for its culture alone whose folder is named for a type of the project,
`Components/Widget/.locales/he.json`.

**Why it matters.** A folder is a namespace, so the file lays the type out as one, and a reader
has to know the folder is a type to tell what the file localizes. The runtime reads it all the
same.

**What fixes it.** Name the file for the type among its namespace folder's locale files,
`Components/.locales/Widget.he.json`.

**Silent where.** A host's folder, which names other assemblies' types the compilation cannot
tell from namespaces; a locale pack's folder is read for the project it names, and held to that
project's types.

## Callsites (LOC0200–LOC0299)

Callsites are found semantically where the compilation binds `IStringLocale`, and syntactically
otherwise; the syntactic path cannot resolve overloads, so the rules that count values stay
silent for it. A key written as a constant is folded by the compiler and checked like a literal
on the semantic path. A lookup inside a type that implements `IStringLocale` is left alone
throughout, since such a type forwards the key its own caller wrote.

Values pair to a key's distinct holes by position, in the order the holes first appear, and a
plural expression's bracketed argument names a value the way a brace hole does:

```csharp
L["Stored {stored} of {total}.", written, allowed]        // right: one value per distinct hole
L["Stored {stored} of {total}.", written]                 // LOC0201: two holes, one value
L["Stored {stored} of {total}.", written, allowed, size]  // LOC0201: two holes, three values
L["Stored {total} of {total}.", total]                    // right: a repeated hole counts once
L["Saved."]                                               // right: no holes, no values
L[$"Stored {stored} of {total}."]                         // LOC0211: the key is interpolated
```

### LOC0200 — An expression reads a name nothing supplies

**Reports.** A plural expression naming an argument, `[$ [count] plural; …]`, whose key names no
such hole and whose lookup supplies no such value.

**Why it matters.** The count reads as zero, silently.

**What fixes it.** Name the argument the lookup supplies.

### LOC0201 — The value count does not match the key's holes

**Reports.** A lookup supplying more or fewer values than its key has distinct holes.

**Why it matters.** The lookup throws.

**What fixes it.** One value per distinct hole; a repeated hole counts once.

**Silent where.** The values arrive as one collection, `object?[]` into
`params ReadOnlySpan<object?>`, whose count only the run knows; an `int[]` in the same position
cannot convert, arrives as one boxed value, and is counted. Also silent on the syntactic path,
where no overload resolved.

### LOC0202 — An expression naming no argument is given no value

**Reports.** A key whose plural expression names no argument, `[$ plural; …]`, looked up with no
value.

**Why it matters.** Such an expression reads the single value the lookup supplies, and throws
without one.

**What fixes it.** Supply the one value.

### LOC0203 — A malformed expression at a callsite

**Reports.** A key written at a callsite whose `[$ … $]` expression does not parse.

**Why it matters.** The expression renders as written, silently.

**What fixes it.** Write `[$ [Name] plural; category: text; …; $]`, the trailing `;` included.

### LOC0204 — A plural expression without `other`

**Reports.** A plural expression that omits the `other` category.

**Why it matters.** A count whose category the expression does not name renders as nothing.

**What fixes it.** Add `other`, which every category the expression leaves out falls back to.

### LOC0205 — A project's Razor markup went unchecked

**Reports.** A project whose `.razor` files produced no generated C#, so their callsites were
not read.

**Why it matters.** The run reads as complete when part of it was never looked at.

**What fixes it.** The linter runs the Razor generator itself; this is the case where Roslyn
loaded but the project produced nothing, and the fallback over `obj/**/generated/` — which needs
`EmitCompilerGeneratedFiles=true` and a full build — came up empty too. Do not ignore it in a
build gate.

### LOC0206 — A key writes text shaped like a hole the runtime cannot read

**Reports.** `{0}`, `{a.b}` or `{stored,x}` in a key at a callsite.

**Why it matters.** The text renders as written; see LOC0110, the same rule in a file.

**What fixes it.** Write the hole as `{Name}`.

### LOC0207 — A key is not answered in a culture its type is translated for

**Reports.** A key written at a callsite that a culture translating its type leaves out, once
per key per culture, at the first callsite writing it. The cultures translating a type are read
from the project's own files and from every locale pack's folder for it alike.

**Why it matters.** Falling back to the key is exactly what the runtime is defined to do, so the
string renders in English and every other rule stays green; adding `L["Details columns"]` to a
component whose `he` and `es` files exist leaves both behind without a word.

**What fixes it.** Translate the key, or declare that its right translation is the source text
with a JSON `null`: `"Email": null`. A null entry is *found* — `ResourceNotFound` is `false`,
`RequireMatchingLocalization` passes, `GetAllStrings` lists it — so a key that has to read as
genuinely missing cannot use it, and the warning is the honest answer there.

**Silent where.** The type has no translations at all, since it is unlocalized rather than
under-translated; the lookup is through the unbound `IStringLocale`, which names no type; a
parent culture answers, since `es-419` reads `es` for what it does not declare; or the run did
not name the project's files.

### LOC0208 — A lookup writes its key as an expression

**Reports.** `L[column.ToString()]`: a key only the run knows.

**Why it matters.** No rule can judge the key and no coverage check can count it; leaving it
silent would let a run read as complete when part of it was never looked at.

**What fixes it.** Write the key as a literal or a constant, which the compiler folds and every
rule reads.

### LOC0209 — A key with holes and no words

**Reports.** A key made only of holes and punctuation, `"{count} / {total}"`.

**Why it matters.** It formats a value rather than says anything, so there is nothing for a
translator to read and no entry a locale file could hold for it; `null` marks a key whose
translation is the source text, and there is no text here to mark.

**What fixes it.** Format the value directly, `$"{count} / {total}"`. A single letter of prose
clears the rule.

### LOC0210 — An expression key with no words in any field

**Reports.** A plural expression whose fields carry only `#`, `"[$ plural; other: #; $]"`.

**Why it matters.** As LOC0209: a number formatted, not a sentence translated.

**What fixes it.** `count.ToString("N0", culture)`, or a field with a word in it.

### LOC0211 — A key written as an interpolated string with holes

**Reports.** `L[$"Stored {stored} of {total}."]`.

**Why it matters.** The string formats before the lookup, so the runtime receives the formatted
text as the key, which no translation is keyed by.

**What fixes it.** A literal key with the values beside it, `L["Stored {Stored} of {Total}.",
stored, total]`. An interpolated string with no holes folds to a constant and is checked like a
literal.

### LOC0212 — A lookup writes a key shaped like a reserved entry

**Reports.** `L["$import"]`: `$` followed by one lowercase word, which the runtime reserves.

**Why it matters.** No file can answer it, since a file naming it fails to load, so the lookup
always falls back to the key.

**What fixes it.** `L["$$import"]` for the text `$import`; see LOC0115.

### LOC0213 — A lookup supplies `string.Format`'s format text

**Reports.** `string.Format(L[key], value)`: the lookup written as the format text, whether by
position or by the `format` name.

**Why it matters.** The runtime already fills a lookup's holes, so `string.Format` reads the
translated result as a second composite format string. `"Stored {0} of {1}."` survives by
accident, since `{0}` is also an index; `"Stored {stored} of {total}."` throws `FormatException`
the moment any culture is active.

**What fixes it.** Pass the values to the lookup and drop `string.Format`.

**Silent where.** The lookup is a value beside the format text, `string.Format("Found {0}.",
L[key])`; or the result is stored, formatted later or passed through a method, which the rule
cannot follow. Without a bound compilation, only the lookup written as the unnamed or
`format`-named first argument is certain.
