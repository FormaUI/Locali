# Linting rules

Every rule `locali` reports, one entry each. An entry says what the rule reports, why it
matters at run time, what fixes it, and where it deliberately stays silent. The linting
configuration reports under 100, a file rule in the 100s, and a callsite rule in the 200s, so
the range names the family.

An **error** is something the runtime will get wrong; a **warning** is something worth changing
that still loads. A default run reports errors; `--severity warning` reports both.

## Linting configuration (LOC0020–LOC0099)

Two files configure two things. `.locali.json` is the linter's own, and a run reads one: the
nearest found at or above the path the run was given, so the solution, a project, and a single
file all lint under the same file, and one beneath it is not read and is reported. The runtime
never reads it. `.locale.json` is the runtime's: one optional file per assembly, at its
project's root, which the linter reads through the runtime's own reader and never writes a key
of its own into.

```json
{
  "localeSources": {
    "**": ["pack", "host"],
    "src/Forma.AddOns/*": ["embedded"],
    "src/Forma.Blazor.Showcase/**": ["embedded", "pack", "host"]
  },
  "ignoreDiagnostics": {
    "**/Forma.Blazor.Showcase/**/LocalizationPage.razor": ["LOC0207"]
  },
  "excludeFiles": ["*/Fixtures/*"],
  "hints": {
    "localeMembers": [
      {
        "assembly": "Forma.Blazor",
        "type": "Forma.Blazor.Components.FormaComponentBase",
        "member": "L",
        "typeMember": "LocaleSourceType"
      }
    ],
    "packages": ["FormaUI.NET-Blazor"]
  }
}
```

`ignoreDiagnostics` pairs a glob with the rules a file it matches is left out of, and is the one
place a per-file exception is written, since a locale file holds entries and nothing a tool
reads. `excludeFiles` mirrors `--exclude`, so a fixture folder is excluded once rather than on
every invocation. Globs are case-insensitive, written with forward slashes, `*` and `**` both
spanning separators, and read relative to the folder the file sits in. Comments and trailing
commas are allowed, as in a locale file. Leaving a diagnostic out is never silent: it is counted
in `suppressedCount` and named in the console brief.

`localeSources` pairs a glob with the places a locale file it matches may come from, any of
`embedded`, a file the assembly embeds in itself, an application's for other assemblies
included; `satellite`, one the build moves into a satellite assembly; `pack`, one a locale pack
carries; and `host`, one in the `.locales` folder at the project's root that the project copies
beside the application rather than embeds. An allow list cannot be merged, so the last line
matching a file speaks for it: lines are written general first and specific after, and a line
that allows nothing, `"**": []`, put first, makes every locale file one a later line has to
claim. A file no line matches has no policy. A locale pack's file is matched where it stands in
the project it localizes, the one building the longest assembly name its name starts with, at
its path in the pack with that assembly's folder left out, so the line over that project says
whether its entries may come from a pack; a file naming no project of the run is matched where
it sits. LOC0025 reports a file from a place its line does not allow, and LOC0021 a line that is
the last match for no file.

`hints` states what the linter cannot read in the code, and changes nothing the runtime does.
`localeMembers` lists members whose locale is read for the type another member of the same
instance returns: the assembly and the type's full metadata name, ``FormaInputBase`1`` for a
generic, declaring the member; `member`, the property returning `IStringLocale`; and
`typeMember`, the property returning `Type`, on that type or one it derives from. A hint is
written for that one declaration: a member overriding or hiding it is a declaration of its own,
read by the flow alone, while an instance that keeps it reads the type member as its own type
overrides it, in source or in the compiled code of the assembly the build runs, so
`typeof(Panel)` names `Panel` and `GetType()` the instance's own type. `packages` names packages
whose hints the run reads.

A library keeps the hints for its own members in a `.locali-hints.json` at its project's root,
an object holding a `localeMembers` list, and ships it at its package's root: `<None
Include=".locali-hints.json" Pack="true" PackagePath="" />`. A run building the library reads it
for each project of the run, unasked, so the hints are held to the code they describe; an
application reads the copy a package carries where its `.locali.json` names the package in
`packages`, from where each project of the run restored the version it references. A
repository's `.locali.json` never travels with its packages.

The flow follows a hinted member without any hint first, so no hint vouches for itself or
another. Where it reaches the member's type on its own, the hint is verified when it reads the
same type and set aside, which LOC0029 reports, when it reads another; where it reaches none, as
over a compiled library, the hint's type is the one read, and a finding resting on it names the
hint. LOC0028 reports a hint naming what is not there, LOC0029 two hints relating one member to
different type members, which leaves it to neither, and LOC0021 a hint naming an assembly the
run neither builds nor references, or a package no project of the run references.

### LOC0020 — The linting configuration cannot be read

**Reports.** A `.locali.json` or a `.locali-hints.json` that is not readable JSON or does not
follow its shape, or a `.locali.json` naming a rule this linter does not define, in either list.

**Why it matters.** A typo in a rule identifier would silence nothing and say nothing; the file
is refused instead. The rule over `.locali.json` itself can never be named in its own
`ignoreDiagnostics` list.

**What fixes it.** Correct the file; the message names the key or the identifier it refused.

### LOC0021 — An entry applied to nothing

**Reports.** A glob in `ignoreDiagnostics` or `excludeFiles` that matched no file the run
reached, a `localeSources` line that is the last match for no locale file the run reached,
because none matches it or a later line claims every file it matches, a hint naming an assembly
no project of the run builds or references, or a package in `hints` no project of the run
references.

**Why it matters.** A file moved out of the tree is exactly when its exceptions go stale, and an
exception that names nothing sits unnoticed until it excuses the wrong file. A `localeSources`
line outlives the files it was written for the same way, or is written after a broader line
that already claims its files, and then reads as a policy nothing is held to.

**What fixes it.** Delete the entry, point it at the file's new place, or move the line after
the broader one that overrides it.

**Silent where.** Nowhere by design: a narrower `--path` leaves the entries for the rest of
the tree matching nothing, and they are reported all the same, which is why the rule is a
warning rather than an error and why a gate lints the whole solution. A `localeSources` line
counts as applied for every locale file it is the last match for, whether or not the file's
source could be told.

### LOC0022 — The runtime's config file is wrong or misplaced

**Reports.** A `.locale.json` that is not readable JSON, names a key the runtime does not read,
gives a value the runtime would refuse, is one of two embedded for an assembly, is embedded
anywhere but the project's root, sits in a host's `.locales` folder, or sits anywhere in a
locale pack.

**Why it matters.** The runtime passes over a key it does not read, so a misspelled one steers
nothing; it refuses every lookup in an assembly that embeds two config files; it reads a file
embedded beneath the root as the assembly's all the same, so the one place it may sit is stated
at lint time; and it reads no config from a host's folder or a locale pack at all, reading each
of their names from the root namespace the assembly it names declares for itself.

**What fixes it.** Keep the keys to `rootNamespace`, `rootDir`, `localesDir` and `paths`, each
as the runtime spells it; a malformed path is reported at the token that is wrong. Embed one
file, at the project's root, and lay none in a host's folder or a locale pack.

**Silent where.** The file reads cleanly and sits where it belongs. The type-bound rules then
read the project's files through it, as the runtime does.

### LOC0023 — The root namespace holds none of the project's types

**Reports.** The root namespace the runtime counts a type's name from, `rootNamespace` in the
project's `.locale.json` or the assembly's name where none is declared, when no type of the
project is under it. A declared one is reported at the config file; an undeclared one at the
project, and only where the project embeds a locale file of its own, any but an application's
file whose longest assembly name is another's, or a locale pack places a file for it under the
root and nowhere else.

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
a locale pack's files for the project spell full type names rather than the root, which the
runtime reads for a type outside it; or the config file will not read, which is LOC0022's.

### LOC0024 — A pack file names an assembly the solution does not have

**Reports.** A culture-named file of a locale pack, a project declaring `IsLocalePack`, whose
name, read without its culture, starts with no assembly a project of the solution builds or
references. It is reported at the file, naming what the file stands for.

**Why it matters.** A pack's file names are the only thing that says which assemblies it
localizes, and the runtime matches a name to an assembly by its start and nothing else. A
misspelled name loads with the pack, answers no lookup, and every type it was written for renders
in the source language without a word.

**What fixes it.** Start the name with the assembly's exactly, `Forma.Blazor.Composer` rather than
the namespace its types share, in a folder, `Forma.Blazor.Composer/he.json`, or after a culture
folder, `he/Forma.Blazor.Composer.json`.

**Silent where.** The run named a project or a directory rather than a solution, since only a
solution lists every project a name could start with; a project of the solution did not compile,
or is written in a language the linter does not read, so its assembly's name cannot be told; the
name starts with an assembly a project references from a package, whose types are out of reach;
or the pack's items could not be read.

### LOC0025 — A locale file from a source the configuration does not allow

**Reports.** A culture-named locale file that comes from a source the last `localeSources` line
matching it does not name: embedded in the assembly, moved into a satellite assembly, carried by
a locale pack, or copied beside the application from the `.locales` folder at the project's
root. A pack's file is held to the line over the project it localizes, so a pack's files for an
assembly whose line allows embedding alone are reported one by one. It is reported at the file,
naming the source it comes from and the sources the line allows.

**Why it matters.** The runtime reads every source it finds, so a repository that keeps its
entries in packs cannot tell from a build that someone embedded a Hebrew file in an
assembly again: both load, the pack wins where they share a key, and the stray file answers
alone where they do not. The rule turns the repository's choice of where entries live
into something the gate holds it to.

**What fixes it.** Move the file to a source its line allows, or add a later `localeSources`
line for the tree that keeps its files there, as an application does for the strings it embeds
itself.

**Silent where.** No `localeSources` line matches the file, since the policy is the
repository's to declare; the file is not named for a culture; the project's items could not be
read, so whether it embeds the file cannot be told; or the project neither embeds the file nor
keeps it in its host folder, or a satellite assembly holds it under a name whose culture is not
the satellite's, which LOC0124 reports, so the runtime never reads it and no source answers for
it. It is a warning because the file loads.

### LOC0026 — A `.locali.json` the run does not read

**Reports.** A `.locali.json` found beneath the path the run was given, other than the one the
run reads. It is reported at that file.

**Why it matters.** A run reads one `.locali.json`, the nearest at or above the path it was
given, and merges nothing from any other. A second file beneath it was written expecting to
apply, and every exclusion, exception, and source it names applies to nothing.

**What fixes it.** Move its entries into the file the run reads, with paths written from
there, and delete it.

**Silent where.** The only `.locali.json` beneath the path is the one read. A run given a
folder above every configuration reads none, and then reports each it finds.

### LOC0027 — An assembly named for a culture

**Reports.** A part of the root namespace the build embeds a project's locale files under, the
assembly's name unless the project names another, that is a culture written exactly as .NET
names it, `Contoso.my.Widgets`, where the project embeds a file under it. It is reported at the
project, naming the part and a file the runtime misreads.

**Why it matters.** The runtime takes the first part of a file's name that is a culture as the
file's culture, and an embedded file's name starts with the root namespace, so every file the
project embeds is read for that culture instead of its own, and a type renders in the source
language for every reader of the culture its files were written in.

**What fixes it.** Rename the assembly or the root namespace, here, where it is built, the one
place the name can change.

**Silent where.** The part is a culture in another case, `He`, which .NET does not name; the
project embeds no locale file under the root namespace, or its items could not be read; every
file it embeds there is one a satellite assembly holds under a name whose culture is not the
satellite's, which the runtime never reads and LOC0124 reports; the project did not compile; the
project is a locale pack, whose files are embedded under their paths alone; or the assembly's
name holds the culture and the root namespace another name, since the files the name misreads
are then an application's or a pack's, and LOC0024 reports a pack's.

## Locale files (LOC0100–LOC0199)

A JSON file is read as a locale file when it carries `$localization`, or is named as one: it
sits in a folder of locale files with a name giving it a culture, or its file name ends in a
culture. A folder of locale files is the one the project's `.locale.json` names as `localesDir`,
`.locales` where it names none or no project sits above the file, and the host's `.locales`
folder at the project's root.
Its culture is read as the runtime reads it, from the project's folder down: the first part of
its name, folders included, that is a culture written exactly as .NET names it, and a part
repeating it is that culture again rather than part of the name. A locale file opens with
`$localization`, then `$shared` where it carries one, then its entries and sections.

### LOC0028 — A hint names what is not there

**Reports.** A hint whose type is not one its assembly declares, whose member is not a property
of that type returning `IStringLocale` or is abstract, so every instance reads an override or an
implementation the hint was not written for, or whose type member is not a property of that
type, or of one it derives from, returning `Type`; or a package `packages` names that carries no
`.locali-hints.json`. It is reported where the hint or the package is written.

**Why it matters.** A hint naming nothing relates nothing, and the lookups it was written for go
unchecked without a word, which is exactly what a typo in a member's name or a renamed type
would do.

**What fixes it.** Correct the name the message quotes, from the type's full metadata name in
the assembly that declares it; ship the library's `.locali-hints.json` in its package, or drop
the package from `packages`.

**Silent where.** The hint names an assembly the run neither builds nor references, which
LOC0021 reports, since the hint may be written for a run that does.

### LOC0029 — A hint disagrees

**Reports.** Two hints relating one member to different type members, at each; or a hint the
flow contradicts: where the flow, followed without hints, reaches one type for the member's
locale on an instance, and the type member the hint names reads another, once per hint, naming
both and the instance.

**Why it matters.** A run over the library's source checks the flow's type, but a run that
cannot follow the flow, as one over the compiled library in an application, has the hint alone,
and would hold every lookup through the member to the wrong type's files. Two disagreeing hints
leave the member to neither, so its lookups go unchecked.

**What fixes it.** Relate the member to the type member its locale is really read for, or delete
the hint the code no longer follows.

**Silent where.** The flow reaches no type on its own, or the type member tells none, since
nothing then contradicts the hint; the instance overrides the hinted member, whose override the
hint was not written for; or hints repeat one relationship, as a package's and an application's
may.

### LOC0100 — Not readable JSON

**Reports.** A file carrying `$localization`, or named as a locale file, that is not readable
JSON.

**Why it matters.** The runtime refuses the file, and every type it would have localized falls
back to the source text.

**What fixes it.** Make it JSON. A `//` or `/* */` comment and a trailing comma are accepted,
as the runtime's own reader accepts them.

### LOC0101 — No metadata entry

**Reports.** A file named as a locale file, in a folder of locale files or with a file name
ending in a culture, that carries no `$localization` entry.

**Why it matters.** The entry is what marks a file as a locale file; without it the build embeds
the file in no assembly, a pack included, and the runtime reads nothing from it beside the
application.

**What fixes it.** Open the file with `"$localization": "SchemaVersion: 1.0.0; Culture: he;"`.

**Silent where.** The file sits outside every folder of locale files and its file name ends in
no culture, so without the entry it is no locale file.

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

**Reports.** A `Culture` other than the one the file's name gives it: the first part of the
name that is a culture, a folder, `.locales/he/Components.json`, or the end of the file's name,
`Components.he.json`, read from the project's folder down.

**Why it matters.** The runtime refuses the file: the name decides which culture the file is
read for, and the entry has to agree.

**What fixes it.** Make the entry say what the name says.

### LOC0109 — A text fills a hole its key does not name

**Reports.** A text writing `{stord}` for a key that names `{stored}`.

**Why it matters.** The runtime renders `{stord}` literally, in front of a user, and nothing else
complains.

**What fixes it.** Name the holes the key names. A name may be written as a brace hole or as a
plural expression's bracketed argument, the two are equivalent, and a text may reorder or
drop a name freely.

### LOC0110 — A text writes something shaped like a hole the runtime cannot read

**Reports.** `{0}`, `{a.b}` or `{stored,x}` in a text: a shape that could only have been
meant as a hole, whose name the runtime cannot read or whose alignment is not a number.

**Why it matters.** The text renders as written.

**What fixes it.** Write the hole as `{Name}`, optionally `{Name,alignment}` or
`{Name:specifier}`.

**Silent where.** The text contains a space, as `{ color: red }` or `{colour, red}` does: that
is prose, rendered as written and meant to be. LOC0206 is the same rule at a callsite.

### LOC0111 — A duplicate key

**Reports.** A key declared more than once in one section.

**Why it matters.** Every text but the last is dropped, silently.

**What fixes it.** Keep one. A key repeating across sections is two types sharing a string, which
LOC0113 reads instead.

### LOC0112 — A file or section names a type that does not exist

**Reports.** A file no type reads, a section naming no type, or the entries outside any section
in a file read for its sections alone; and a file no path in force places. Every report names
the anchors the runtime places the file at, and a library's file whose name starts with an
assembly's name is reported as one only an application or a locale pack reads by that name.

**Why it matters.** The runtime reads entries by the owning type's full name, from a
file's position and the sections inside it, so a moved folder, a renamed type or a misspelled
section strands its entries while every other check stays green.

**What fixes it.** Put the file where the type's name says, or name the type in a section: from
the file's position, or from the assembly's root with a leading `@.`. The rule reads through the
runtime's own source, from the root down to the type, for every type the project declares and
every culture its files carry, under the name the build gives each file, an explicit
`LogicalName` included, and through the `.locale.json` the project embeds. A locale pack's files
whose longest assembly name is the project's are read the same way, from the project's root
namespace, so a pack's file is reported where it sits in the pack. An application's file is read
as well for every assembly of the run its name starts with, from that assembly's root namespace,
and held to that assembly's types.

**Silent where.** The run did not name the project's files; the file is a host's, one the
project does not embed; the project is an application, an executable or a library its build
marks as one, and the file's name starts with the name of an assembly no project of the run
builds, whose types are out of reach, or goes on, past the longest assembly name it starts with
or the application's own root, into a part that starts no namespace of that assembly's types,
which an assembly the run does not know, loaded at run time, could start; or the file is a
locale pack's whose name starts with no assembly a project of the run builds, which LOC0024
reports where it starts with none the solution builds or references.

### LOC0113 — A key worded two ways across sections

**Reports.** Sections of one file that give the same key different texts, each after the first
naming the section it differs from. A `null` reads as the key text, and the last declaration in
a section is the one compared.

**Why it matters.** Both load, so the same source text reads two ways across the application.

**What fixes it.** Agree the texts, or say in `.locali.json` that they differ on purpose,
where a text agrees in gender or number with the thing its type describes. That case is
why the rule is a warning.

### LOC0114 — A reserved entry out of order

**Reports.** A `$shared` before `$localization`, or either after an entry or a section.

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

**Why it matters.** The runtime refuses the file. `$shared` holds entries alone, which every
type reached through its node gets unless a section of its own says otherwise.

**What fixes it.** Keep entries only in it; put a nested section beside it.

### LOC0118 — A folder named for a type

**Reports.** A file named for its culture alone whose folder is named for a type of the project,
`Components/Widget/.locales/he.json`.

**Why it matters.** A folder is a namespace, so the file lays the type out as one, and a reader
has to know the folder is a type to tell what the file localizes. The runtime reads it all the
same.

**What fixes it.** Name the file for the type among its namespace folder's locale files,
`Components/.locales/Widget.he.json`.

**Silent where.** A host's folder, which names other assemblies' types the compilation cannot
tell from namespaces; a locale pack's file is read for the project building the longest
assembly name its name starts with, and held to that project's types.

### LOC0119 — A type answered in one culture family and left out of another

**Reports.** A type with entries for one culture family among the project's files and packs,
none at all in another family the project's files or packs also carry: once per type per family
missing it, at a file that does answer the type.

**Why it matters.** A type's entries used to sit beside each other, one file per culture,
where a language falling behind was visible by eye. Split across packs, a type Hebrew answers
and Spanish never received loads correctly and falls back to the source language for every
Spanish reader, with every other check staying green.

**What fixes it.** Localize the type in the missing family, or accept that it renders in the
source language there.

**Silent where.** The project's files and packs carry only one culture family; the type has no
entries in any family, since it is unlocalized rather than uneven; a regional culture,
`es-419` among them, answers where its neutral culture, `es`, does not, and the reverse, since
one stands for the family; or the only entries a culture carries for the type come from a
`$shared` block above it, a folder's or an outer type's, which answers for every type beneath
and says nothing about this one. A `$shared` block in the type's own section or file is the
type's own and counts.

### LOC0120 — Two files give an entry different texts

**Reports.** An entry, a key a type reads in one culture, that two files of one source give
different texts, at the file whose name sorts first, naming the other. A shared entry is reported
for the namespace whose `$shared` holds it. The rule collects every type's entries as the runtime
does, from a project's embedded files as one source and each locale pack's files for the project
as another; an application's files are read as well for every assembly of the run their names
start with.

**Why it matters.** Two files of one source may stand for one name, a culture folder and a
folder's own `.locales` among them, and the runtime reads both. Where they disagree it keeps the
text of the file whose name sorts last, ordinally, and logs the conflict once as a warning, so
one of two wordings is shown and the other is never seen.

**What fixes it.** Give the entry one text, or keep it in one file. A later source is no
conflict: a pack's text wins over the assembly's own as the runtime's order says.

**Silent where.** Two files give the entry the same text; no type reads the entry, of the
project or, for an application's file, of the assembly its name starts with, since a section
naming no type is LOC0112's; one file gives it twice, which LOC0113 reports; the files belong to
two sources; or the files are copied beside the application, a folder the runtime merges from
every project's copies, which no one project shows whole.

### LOC0121 — A name read two ways

**Reports.** An application's file whose name reads both in the application's own namespace and
as a name of another assembly of the run, where a type of each reads one of its sections: in an
application `Epica.App` declaring `Epica.App.Forma.Blazor.Components.Bars.FormaToolbar`,
`.locales/es/Forma.Blazor.Components.Bars.json` with a `FormaToolbar` section answers that type
and `Forma.Blazor`'s `FormaToolbar` alike. It is reported at the file, naming both readings.

**Why it matters.** A lookup reads a file for the type it looks up, so each reading applies where
its type exists, and one section answers two assemblies' types; a key worded for one reads the
same for the other, which nobody chose.

**What fixes it.** Rename the application's namespace that repeats the other assembly's name; a
folder and a dot read alike, so moving the file changes nothing while both names stand.

**Silent where.** The project is a library, whose files are read for its own types alone; no one
section is read for a type of each, though each reading reaches some section; or the other
assembly is no project of the run, so its types are out of reach.

### LOC0122 — A culture written in another case

**Reports.** A part of the name a locale file is embedded under, a folder or a part between dots,
that is a culture written in another case than .NET names it, `ES` or `He` or `en-us`, where the
runtime then misreads the file: the name carries no culture at all, or the part names no type,
namespace or assembly the run knows. It is reported at the file, saying what the runtime makes of
the name, and where the name carries no culture it names a part naming nothing the run knows
before one that names something.

**Why it matters.** The runtime compares a culture exactly, so `.locales/ES/Components.json`
carries no culture and is never read, and `Components.Es.he.json` stands for a type named `Es`
that does not exist, which LOC0112 reports as well; either way its entries are read for no
reader.

**What fixes it.** Write the culture as .NET names it: `es`, `he`, `en-US`.

**Silent where.** The name carries a culture and the part names a type, a namespace or an
assembly the run knows, as `Id` may, which the runtime reads as part of a name on purpose; the
part is in the root namespace, which LOC0027 reads; the file is an application's read for an
assembly whose types are out of reach; the project does not embed the file, or its items could
not be read; the project did not compile, so its types cannot be told; or the project is a locale
pack, whose files name other assemblies' types, which its own project does not show.

### LOC0123 — A second culture in a file's name

**Reports.** A part of the name a locale file is embedded under, a folder or a part between dots,
that is a culture other than the one the name gives the file and names no type, namespace or
assembly the run knows, as `he` is in `.locales/es/he.json` and `es` in
`.locales/he/Components.Widget.es.json`. It is reported at the file, a pack's file in the project
it localizes.

**Why it matters.** The runtime reads the file for the first culture in its name and the second
as part of the name it stands for, so `.locales/es/he.json` holds Spanish entries for a
namespace `he`, which no type reads; LOC0112 reports that as well.

**What fixes it.** Move the file into the folder of the culture it is written for. A part
repeating the file's own culture is that culture again, so `.locales/es/es.json` is read as
`.locales/es.json` is, and `.locales/es/Components.Widget.es.json` as
`.locales/es/Components.Widget.json` is.

**Silent where.** The part names a type, a namespace or an assembly the run knows, which the
runtime reads as part of a name on purpose; the file is an application's read for an assembly
whose types are out of reach; the project does not embed the file, or its items could not be
read; a satellite assembly holds the file under a name whose culture is not the satellite's, so
the runtime never reads it, which LOC0124 reports; or the project did not compile, so its types
cannot be told.

### LOC0124 — A satellite's file named for another culture

**Reports.** A locale file the build moves into a satellite assembly, which the SDK does for an
`EmbeddedResource` item whose file name ends in a culture and that does not set
`WithCulture="false"`, under a name whose first culture is not the satellite's, as the `he`
satellite holds `.locales/es/Widget.he.json` under `Contoso.Widgets/.locales/es/Widget.he.json`.
It is reported at the file, naming the satellite's culture, the name and the culture it gives.

**Why it matters.** The runtime reads a satellite's file for the satellite's culture, and lists
one whose name holds another culture in no culture at all, so none of its entries is ever read
and every lookup it was written for renders in the source language.

**What fixes it.** Let the localization package embed the file, which keeps it in the assembly
under its path, or give the item a `LogicalName` holding the satellite's culture, or none.

**Silent where.** The name holds the satellite's culture, or none, which the runtime reads the
satellite's culture into; the build keeps the file in the assembly itself; or the project's items
could not be read.

## Callsites (LOC0200–LOC0299)

Callsites are found semantically where the compilation binds `IStringLocale`, and syntactically
otherwise; the semantic path reads the operation tree the compiler binds, which has one shape
for every language it compiles, and the syntactic path reads C# or Visual Basic syntax and
cannot resolve overloads, so the rules that count values stay silent for it. A key written as
a constant is folded by the compiler and checked like a literal on the semantic path. A lookup
inside a type that implements `IStringLocale` is left alone throughout, since such a type
forwards the key its own caller wrote.

A call to a method whose parameter carries `[LocaleKey]` is a lookup too, found on the semantic
path alone: the argument for that parameter is the key, read in the entries of the method's
one type argument, and the values are what one `params` parameter after the key gathers. A key
passed on from a parameter that carries `[LocaleKey]` itself is left alone, since it is checked
where its own method is called:

```csharp
LocalizableText.For<SectionMenu>("{Count} results today", 12)     // right: one value for its one hole
LocalizableText.For<SectionMenu>("{Count} results today", 12, 3)  // LOC0201: one hole, two values
LocalizableText Title([LocaleKey] string key)
    => LocalizableText.For<SectionMenu>(key);                     // right: Title's callers write the key
```

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
where no overload resolved, and for a call through `[LocaleKey]` whose method takes anything but
one `params` parameter after its key, since the method may fill the holes itself.

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

### LOC0207 — A key is not answered in a culture its type is localized in

**Reports.** A key written at a callsite that a culture localizing its type leaves out, once per
key per culture, at the first callsite writing it. A type's entries and cultures are read from
the assembly declaring it, as the runtime reads them: the files of the project of the run that
builds that assembly, an application's files named for its own assembly among them, and every
locale pack's files for it, with the files the lookup's own application writes for that assembly
laid over them.

A lookup's type is found by following its receiver back to where the runtime decided it: a
locale the registered factory creates for a type, a value typed `IStringLocale<T>`, followed
further where a type of the run derives from `T`, since a locale for it may stand behind one for
`T`, or a `[LocaleKey]` method's type argument, through locals, fields, properties, parameters
at each call the run makes, constructors, lambdas, local functions, delegates, lazy values,
virtual calls on each type the instance can be, `base.` calls to the member they name, and
services to the one implementation the run's product code registers. Where the walk reaches no
type, a hint in the linting configuration may relate a member to the type member its locale is
read for, read as each type of instance overrides it: `typeof(X)` names `X`, and `GetType()` the
instance's own type. The instances are the lookup's own type, unless it is abstract, and every
type of its project derived from it that keeps the member holding the lookup as it is. So
`L["Details columns"]` in a component deriving from `FormaComponentBase` is held to the
component's own files wherever Forma comes from, and a helper reading a locale its callers pass
is held to each caller's type. The message names the path the type was reached through, and the
hint where a hint alone supplied it.

A lookup through Microsoft's `IStringLocalizer<T>` with a literal key is held to `T`'s entries
where the run has Forma's adapter answer it: its product code calls
`AddFormaStringLocalizerAdapter` ahead of any call registering Microsoft's localization,
`AddLocalization` or `AddViewLocalization` among them, in the same block, and registers neither
localizer contract itself. No other rule reads such a lookup, since the localizer fills a
composite key with `string.Format`.

**Why it matters.** Falling back to the key is exactly what the runtime is defined to do, so the
string renders in the source language and every other rule stays green; adding
`L["Details columns"]` to a component whose `he` and `es` files exist leaves both behind without
a word.

**What fixes it.** Localize the key, or declare that its right text is the source text with a
JSON `null`: `"Email": null`. A null entry is *found* — `ResourceNotFound` is `false`,
`RequireMatchingLocalization` passes, `GetAllStrings` lists it — so a key that has to read as
genuinely missing cannot use it, and the warning is the honest answer there.

**Silent where.** The type has no entries at all, since it is unlocalized rather than
under-localized; no project of the run builds the type's assembly, so none of its entries can be
read; the walk from the lookup reaches no type, as where a type is chosen at run time, a locale
comes from a factory the code builds itself or a string-named one, a local is assigned more than
once or by a deconstruction, a field is written where the walk cannot pair the write with the
object read, or a service has no one implementation the walk can read, as where it is registered
twice, as an instance, through a factory creating no one type, or by a call of the application's
own handed the service collection, or declared outside the run's projects; the types the walk
reaches are alternatives it cannot pair with the lookup, as both sides of a conditional, several
returns, or a value a constructor or a later write may replace; the type member a hint names
returns nothing the walk can read, which LOC0214 reports; a type derived from the lookup's own
overrides the member holding the lookup, so its instances never run it; the lookup goes through
Microsoft's `IStringLocalizer` where the run does not have Forma's adapter answer it, so it
reads the localizer's own resources; a parent culture answers, since `es-419` reads `es` for
what it does not declare; or the run did not name the project's files.

### LOC0208 — A lookup writes its key as an expression

**Reports.** `L[column.ToString()]`: a key only the run knows.

**Why it matters.** No rule can judge the key and no coverage check can count it; leaving it
silent would let a run read as complete when part of it was never looked at.

**What fixes it.** Write the key as a literal or a constant, which the compiler folds and every
rule reads.

**Silent where.** The key is a parameter that carries `[LocaleKey]` itself, which the method's
own callers write and the rules read there.

### LOC0209 — A key with holes and no words

**Reports.** A key made only of holes and punctuation, `"{count} / {total}"`.

**Why it matters.** It formats a value rather than says anything, so there is nothing for anyone
localizing it to read and no entry a locale file could hold for it; `null` marks a key whose
text is the source text, and there is no text here to mark.

**What fixes it.** Format the value directly, `$"{count} / {total}"`. A single letter of prose
clears the rule.

### LOC0210 — An expression key with no words in any field

**Reports.** A plural expression whose fields carry only `#`, `"[$ plural; other: #; $]"`.

**Why it matters.** As LOC0209: a number formatted, not a sentence localized.

**What fixes it.** `count.ToString("N0", culture)`, or a field with a word in it.

### LOC0211 — A key written as an interpolated string with holes

**Reports.** `L[$"Stored {stored} of {total}."]`.

**Why it matters.** The string formats before the lookup, so the runtime receives the formatted
text as the key, which no entry is keyed by.

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
localized result as a second composite format string. `"Stored {0} of {1}."` survives by
accident, since `{0}` is also an index; `"Stored {stored} of {total}."` throws `FormatException`
the moment any culture is active.

**What fixes it.** Pass the values to the lookup and drop `string.Format`.

**Silent where.** The lookup is a value beside the format text, `string.Format("Found {0}.",
L[key])`; or the result is stored, formatted later or passed through a method, which the rule
cannot follow. Without a bound compilation, only the lookup written as the unnamed or
`format`-named first argument is certain. A call through `[LocaleKey]` is never read as format
text, since what its method returns is its own.

### LOC0214 — A lookup's type through a hinted member could not be read

**Reports.** A lookup through a member a hint relates to a type member, where the flow reaches
no type on its own and the type member, as a type of instance reads it, has a getter that
returns no `typeof` of a type or `GetType()` of the instance that could be read, once per lookup
and type member, naming the types of instance it was left unread for: `protected override Type
LocaleSourceType => _source;`. A getter is read in source, in another project of the run's
source, or in the compiled code of the assembly the build runs, where it returns one of the two
in one straight run.

**Why it matters.** LOC0207 holds the key to the type the getter names; where no reading tells
the type, the key goes unchecked, and leaving that silent would let a run read as complete when
part of it was never looked at.

**What fixes it.** Return the type as `typeof(X)`, or the instance's own as `GetType()`, from
the getter; or, where the type is chosen at run time, accept the key unchecked.

**Silent where.** The project is localized in no culture, by its own files or a pack, so no key
of it could be checked; the flow reaches the member's type without the hint; the lookup goes
through the generic `IStringLocale<T>`, through a member no hint names, or through another
instance than the one its code runs on, which LOC0207 leaves alone.
