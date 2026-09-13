# locali

A static-analysis linter for Forma.Framework.Localization.Json, validating translation resources and C#/Razor call sites for malformed expressions, placeholder mismatches, missing translations, and silent runtime fallbacks.

Two things go wrong quietly in a localized codebase, and a build catches neither:

- **A lookup throws, or renders the wrong thing.** `L["Stored {stored} of {total}.", 1]`
  supplies one value for two holes and throws at run time. A Hebrew translation that writes
  `{stord}` renders those seven characters literally, in front of a user.
- **A string silently stays in English.** Adding `L["Details columns"]` to a component whose `he`
  and `es` files already exist leaves both files behind — and falling back to the key is exactly
  what the runtime is defined to do, so nothing anywhere complains.

The linter reads translation files against the schema, and reads every `IStringLocale` call site in
C# and Razor against what the runtime will do with it. [Rules](#rules) lists what it reports.

## Quick start

The binary is `locali`. The project is not packable, so run it from source:

```powershell
dotnet run --project src/Forma.Locali -- --path <target>
```

Lint a whole solution, skipping the test fixtures that are malformed on purpose:

```powershell
locali --path ../Forma/Forma.slnx --exclude "*/Fixtures/*"
```

Errors are reported by default. To read the warnings as well:

```powershell
locali --path ../Forma/Forma.slnx --exclude "*/Fixtures/*" --severity warning
```

The examples below use `locali` for the built binary.

## Options

```text
Usage: locali --path <file-or-directory> [--report [<path>]] [--exclude <glob>]
              [--severity <level>]

  --path     The file or directory to lint. A directory is searched recursively.
  --report   Write a JSON report. Defaults to localization-report.json in the
             current directory; a path ending in .json names the file, and any
             other path names the directory to write the default name into.
  --exclude  Leave every file whose path matches the glob unread. May be given
             more than once. Matching is case-insensitive against the whole
             path written with forward slashes, and '*' spans separators, so
             '*/Fixtures/*' skips a folder wherever it sits.
  --severity The lowest severity to report: 'error' or 'warning'. 'warning'
             includes both warnings and errors. Defaults to 'error'; omitted
             warnings do not fail the run, and the summary briefs them by rule.
```

`--path` accepts a `.json` file, a `.cs` or `.razor` file, a `.csproj`, a `.sln`/`.slnx`,
or a directory. A directory is searched for `.json`, `.cs`, and `.razor`, skipping `bin`
and `obj`. Source files are linted through the project that owns them, found by walking
up to the nearest `.csproj`.

`--exclude` is what makes a test tree lintable. A test project's fixtures are often
malformed on purpose — a file with no `$localization` entry, a culture that disagrees with
its own name — so linting them reports what the fixture exists to prove. Excluding the
folder they live in leaves the rest of the tree checked.

`--severity` decides what counts as a finding. An error is something the runtime will get
wrong; a warning is something worth changing that still loads. `warning` includes both,
since `LintSeverity` is declared in ascending order and the comparison is `>=`.

## Output

Console output is MSBuild-formatted, so an editor can navigate it:

```text
path(line,col): severity CODE: message
```

A diagnostic below `--severity` is left out of the console, the JSON report, and the exit
code alike, but a default run still briefs what it left out, so a warning is never silently
dropped:

```text
Linted 658 file(s); 6 diagnostic(s).

8 warning(s) not shown:
  - LOC0106 (1)
  - LOC0205 (1), LOC0209 (4), LOC0210 (2)
Pass --severity warning to read them.
```

The brief names each rule and how many it contributed, a line per family, so a run says
whether the warnings are one rule worth looking at or noise already known about.

| Exit code | Meaning |
|-----------|---------|
| 0 | Every file linted clean at the reported severity |
| 1 | At least one diagnostic was reported |
| 2 | The command line was wrong, or a path could not be read |

`--report` writes a JSON report carrying `fileCount`, `diagnosticCount`, `suppressedCount`,
and a `diagnostics` array of `path`, `line`, `column`, `severity`, `code`, `message`.

## How call sites are found

Call sites are found semantically where the compilation binds `IStringLocale`, and
syntactically otherwise. The syntactic path cannot resolve overloads, so the rules that
count values stay silent for it.

Almost every call site in this repository lives in `.razor`, which reaches Roslyn only
through the Razor source generator. The linter runs that generator itself, so nothing has
to be emitted to disk and no build property is needed:

```powershell
locali --path MyProject.csproj
```

LOC0206 covers the narrower case where Roslyn did load but a project produced no generated
Razor. The linter then reads whatever a build left in `obj/**/generated/`, which needs
`EmitCompilerGeneratedFiles=true` and a full build — an incremental one emits nothing while
still reporting success. LOC0206 says that route came up empty too, so the project's markup
went unchecked; do not ignore it in a build gate.

Loading Roslyn is itself a workaround, described under
[Roslyn is loaded from the SDK](#roslyn-is-loaded-from-the-sdk).

## Rules

A file rule reports in the 100s, a call-site rule in the 200s.

### Translation files (LOC0100–LOC0199)

| Code | Severity | Reports |
|------|----------|---------|
| LOC0100 | Error | The file is not readable JSON |
| LOC0101 | Error | The file carries no `$localization` entry |
| LOC0102 | Warning | The metadata entry is not the file's first property |
| LOC0103 | Error | The metadata entry does not follow the field grammar |
| LOC0104 | Error | The metadata entry omits a required field |
| LOC0105 | Warning | The metadata entry declares its required fields out of order |
| LOC0106 | Error | The file is written for a schema this build does not define |
| LOC0107 | Warning | The metadata entry declares an unknown field |
| LOC0108 | Error | The file declares a culture its own name does not give it |
| LOC0109 | Error | A translation fills a hole its key does not name |
| LOC0110 | Error | A translation writes text shaped like a hole the runtime cannot read |
| LOC0111 | Error | The file declares the same key more than once |
| LOC0112 | Error | A translation file, or a section of one, names a type that does not exist |

LOC0112 covers the drift a rename opens: the runtime resolves translations by the owning
type's full name, from a file's position and the sections inside it, so moving a folder,
renaming a type, or misspelling a section leaves the translations stranded while every other
check stays green. The rule runs the runtime's own walk, from the file named for the assembly
down to the file named for the type, for every type the project declares and every culture
its files carry, and reports whatever no walk read: a whole file when nothing in it is
reached, a section when it names no type, or the entries outside any section in a file read
for its sections alone. It reads a file under every name the build could give it, allowing
for the leading segments `ResourcesPath` inserts, and names generic and nested types as the
runtime does. A file the project embeds under an explicit `LogicalName` is beyond it, because
that decouples the file's position from its logical name entirely.

LOC0109 catches the silent case: `"Stored {stored}."` translated as `"…{stord}…"` renders
`{stord}` literally. A name may be written as a brace hole or as a plural expression's
bracketed argument, and the two are equivalent, so mixing them across a key and its
translation is legal. Reordering or dropping a name is legal.

### Call sites (LOC0200–LOC0299)

| Code | Severity | Reports | Runtime |
|------|----------|---------|---------|
| LOC0201 | Error | An expression reads a name nothing supplies | counts as 0, silently |
| LOC0202 | Error | The value count does not match the key's distinct holes | throws |
| LOC0203 | Error | An expression naming no argument is given no value | throws |
| LOC0204 | Error | A key carries a malformed expression | renders as written, silently |
| LOC0205 | Warning | A plural expression omits `other` | renders nothing, silently |
| LOC0206 | Warning | A project's Razor markup went unchecked | — |
| LOC0208 | Error | A key writes text shaped like a hole the runtime cannot read | renders as written, silently |
| LOC0209 | Warning | A key is not answered in a culture its type is translated for | renders untranslated, silently |
| LOC0210 | Warning | A lookup writes its key as an expression | unchecked, whatever it reads |
| LOC0211 | Warning | A key carries holes but no words anywhere else | works, but names no translation |
| LOC0212 | Warning | An expression key carries no words in any field | works, but names no translation |
| LOC0213 | Error | A lookup writes its key as an interpolated string with holes | the formatted text becomes the key, silently |

LOC0200 and LOC0207 are retired, and neither number is reused.

LOC0209 catches the drift a new string opens. Adding `L["Details columns"]` to a component
whose `he` and `es` files already exist leaves both files behind, and nothing else notices:
falling back to the key is exactly what an absent entry is defined to do, so the string
renders in English and every other rule stays green.

The type is read from the receiver's `IStringLocale<T>`, so the rule needs the semantic
path; a lookup through the unbound `IStringLocale` names no type to check against. The rule
asks the runtime's own walk what the type reads in each culture the project's files carry,
whether from the file named for the type, a section of its folder's file, or the assembly's
file. Only cultures the type already has translations in are checked, because a component
with no translations at all is unlocalized rather than under-translated. One diagnostic is
reported per key per culture, at the first call site writing it, so a key used in ten places
reports twice and not twenty.

Writing the key with a JSON `null` answers it:

```json
{
  "$localization": "SchemaVersion: 1.0.0; Culture: he;",
  "Email": null
}
```

That is the runtime's own way of saying a key stays in the source language here, which
`JsonResourceCache` reads as source text rather than a missing entry. A key whose right
translation is the source text — a term the language borrows, an acronym, a brand, a symbol
— is declared, not omitted.

The escape hatch is not free: a null entry is *found*, and a found entry behaves differently
from an absent one wherever a caller can tell the two apart:

| | `"Email": null` | key absent |
| --- | --- | --- |
| `ResourceNotFound` | `false` | `true` |
| `RequireMatchingLocalization` | passes | throws |
| `GetAllStrings` | lists it | omits it |

A key that has to read as genuinely missing cannot use `null` for that reason — it would pass
checks meant to catch it — and the warning is the honest answer there instead.

A parent culture answers for its children, because `JsonStringLocale` walks a culture up to
its parents when it looks a key up. A `Widget.es-419.json` carrying only what differs from
`Widget.es.json` is complete, and only a key neither file declares is reported.

LOC0210 names what the other rules could not see. `L[column.ToString()]` reads a key only
the run knows, so no rule can judge it and no coverage check can count it — leaving it
silent would let a run read as complete when part of it was never looked at. It mirrors
LOC0206, which says the same about Razor markup that produced no generated C#.

A key written as a constant is not a blind spot, because the compiler folds it:

```csharp
private const string Retry = "Try again";

L[Retry]                  // read as "Try again", checked like a literal
L["Try " + "again"]       // folded too
L[column.ToString()]      // LOC0210
```

A folded key is a real call site, so every rule reads it and LOC0209 expects a translation
for it. Only the semantic path folds, since the syntactic one has no compiler to ask.

A lookup inside a type that implements `IStringLocale` is left alone. Such a type forwards
the key its own caller wrote rather than writing one, so `_inner[name]` in a decorator or a
test stub is not a blind spot — the key belongs to whoever called it.

LOC0211 and LOC0212 catch a key with nothing for a translator to read. A key made only of
holes and punctuation, such as `"{count} / {total}"`, and a plural expression whose fields
carry only `#`, such as `"[$ plural; other: #; $]"`, both format a value rather than say
anything, so `$"{count} / {total}"` or `count.ToString("N0", culture)` replaces the lookup
outright — there is no translation file entry for either rule to expect, because there is no
prose to translate. A field or a literal run carrying a single letter is enough to clear
either rule, so `"{count} files"` and `"[$ plural; one: # file; other: # files; $]"` are left
alone. A localization file cannot rescue such a key either: `null` marks a key whose
translation is deliberately the source text, and there is no source text here to mark, only a
value with nowhere to put it.

Values pair to the key's distinct holes by position, in the order the holes first appear,
and a plural expression's bracketed argument names a value the same way a brace hole does:

```csharp
L["Stored {stored} of {total}.", written, allowed]        // right: one value per distinct hole
L["Stored {stored} of {total}.", written]                 // LOC0202: two holes, one value
L["Stored {stored} of {total}.", written, allowed, size]  // LOC0202: two holes, three values
L["Stored {total} of {total}.", total]                    // right: a repeated hole counts once
L["Saved."]                                               // right: no holes, no values
L[$"Stored {stored} of {total}."]                         // LOC0213: the key is interpolated
```

LOC0213 catches the key that never reaches the runtime as written: an interpolated string
formats before the lookup, so the runtime receives the formatted text as the key, which no
translation is keyed by. An interpolated string with no holes folds to a constant on the
semantic path and is checked like a literal, and a key composed inside a type that
implements `IStringLocale` is left alone, as under LOC0210.

### Where a rule stays silent

Silence below is the rule working. A false positive in a build gate blocks correct code,
so a rule fires only where the failure is certain.

Reporting these would flag code that is right:

- **A hole repeated twice counts once.** `"{n} of {n}"` takes one value, because that is
  how the runtime pairs them. Counting it twice would report every correct call.
- **A hole no value fills renders as written.** `Resolve` returns a pattern with its holes
  still open for the caller to fill, so an unfilled hole is the feature, not a mistake.
- **Text shaped like a hole, containing a space, is prose.** `{ color: red }`, `{ }` and
  `{colour, red}` render as written and were meant to. LOC0110 and LOC0208 take the other
  half, where the text could only have been a hole: `{0}`, `{a.b}`, and `{stored,x}`, whose
  name the runtime reads but whose alignment is not a number, so the hole goes unread.

Nothing at the call site decides these, so there is nothing to judge:

- **A values collection passed whole.** `object?[]` into `params ReadOnlySpan<object?>`
  converts, so the items arrive as the values and their count is whatever the collection
  holds at run time. An `int[]` in the same position cannot convert and arrives as one
  boxed value, which is why that one *is* counted.
- **An overload nothing resolved.** Only the syntactic path reaches this, and only when
  the compilation did not bind the call. Build with the references in place and the
  semantic path takes over.
- **A translation file the run did not name.** LOC0112 and LOC0209 both read the files a
  project owns, so linting a lone `.razor` file checks its call sites and nothing else.
  Point `--path` at the project, the solution, or a folder to get them.
- **A host's own `locales` folder.** Files under a `locales` folder beside the project
  translate other assemblies' types, which the runtime reads from that folder rather than
  from the assembly. LOC0112 and LOC0209 read a project's embedded files alone; the file
  rules still read every file.

## Contributing

### Layout

| Folder | Holds |
|--------|-------|
| `Cli/` | Argument parsing, options, exit codes, target resolution |
| `Diagnostics/` | `LintCode`, the `LintCodes` catalogue, `LintDiagnostic` |
| `Documents/` | Reading a translation file into ordered entries with offsets |
| `Linting/` | The services that drive file and schema linting |
| `Projects/` | MSBuild loading, and binding Roslyn to the SDK's copy |
| `Reporting/` | Console and JSON reporters |
| `Rules/` | Rule contracts, rule contexts, shared hole-name reading |
| `Rules/Files/` | Rules over a translation file |
| `Rules/Sources/` | Rules over a call site |
| `Sources/` | Finding call sites, and the rules that read a project's translation files whole |

### Adding a rule

1. Add a `LintCode` to `Diagnostics/LintCodes.cs` and to its `All` list, numbered in the
   100s for a file rule or the 200s for a call-site rule.
2. Implement `ILocalizationFileRuleChecker` or `ICallSiteRuleChecker` under
   `Rules/Files/` or `Rules/Sources/`. Expose the code through `Code` and report through
   the context, which owns the position.
3. Register it in `Program.cs`.
4. Register it in the matching test factory under `tests/…/Support/`.

`LintCodesTests` fails if a rule reports under a code missing from the catalogue, if two
codes share an id, if two rules in the same family report under the same code, or if a
rule reports outside its family's range. The range rule means the same idea checked on
both sides of a call — a key and a translation, say — needs two codes, one per family, as
`TranslationHoleShapedText` (LOC0110) and `HoleShapedText` (LOC0208) show. A rule that
needs to report two different things is two rules regardless of family.

`HoleNames` reads the names a format refers to its values by, and matches the runtime's
`LocalizedFormat.Names`. Use it rather than scanning for braces; a plural expression names
its argument in brackets, and escaped braces are literal text.

A rule that depends on how values reach the call site should read
`context.Site.Arguments` and return when it is `Unknown`.

### Build and test

```powershell
dotnet build Forma.Locali.slnx
dotnet test tests/Forma.Locali.Tests
dotnet format Forma.Locali.slnx --verify-no-changes
```

`SourceLinterTestFactory.Lint` compiles a class body against the real
`IStringLocale` and returns what the call-site rules report, so a rule can be tested
against a bound compilation rather than a stub.

Run a new rule over the sibling Forma repository's `src/Forma.Blazor` and
`src/Forma.Blazor.Showcase` before trusting it; both lint clean, so anything it
reports there is a false positive. They do not cover every shape a call site can take —
nothing there passes a values collection whole — so those belong in unit tests.

## Roslyn is loaded from the SDK

This part is a workaround, and nothing above depends on understanding it.

Roslyn refuses to load a generator built against a newer compiler than the host, reporting
`ReferencesNewerCompiler` and returning no generators at all. The Razor generator ships
inside the SDK and is built against the SDK's Roslyn, which is ahead of anything published:

| | Version |
|---|---|
| Razor generator, from the SDK | built against Roslyn 5.9.0 |
| Newest `Microsoft.CodeAnalysis.CSharp` on nuget.org | 5.6.0 |

So the linter cannot load the generator using the packages it compiles against, and
`GetSourceGeneratedDocumentsAsync` returns nothing for Razor. What closes the gap is that
the SDK also carries a complete, version-matched Roslyn — compiler, workspaces and
`Workspaces.MSBuild` — under `DotnetTools`, shipped for `dotnet format` and `dotnet watch`.

`SdkRoslyn` binds to that copy from a `[ModuleInitializer]`, since resolution has to be
registered before the first Roslyn type loads, which is earlier than `Main`. The three
`Microsoft.CodeAnalysis.*` packages carry `ExcludeAssets="runtime"` so their assemblies
stay reference-only and cannot shadow the SDK's at run time.

Redirecting only part of the stack does not work: a 5.6.0 `Workspaces` on a 5.9.0 compiler
throws `TypeLoadException` on members added in the newer version. It is all or nothing.

### When Roslyn cannot be bound

The linter stops with exit code 2 and explains what it looked for. It cannot degrade:
`ExcludeAssets="runtime"` keeps the published assemblies out of the output, so if the
SDK's are not found there is no Roslyn to run on at all. Failing at startup is what keeps
that from surfacing as an unrelated `FileNotFoundException` several frames later.

### Removing it

`DotnetTools` is an SDK implementation detail, not a documented redistribution point, so
this should not outlive the version gap. Once a Roslyn at or above the SDK's is on
nuget.org:

1. Raise the three `Microsoft.CodeAnalysis.*` versions.
2. Drop their `ExcludeAssets="runtime"`.
3. Delete `Projects/SdkRoslyn.cs`, and register MSBuild in `MSBuildProjectLoader` again.

Nothing else depends on it, and the startup failure above stays either way.