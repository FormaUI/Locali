# locali

A linter for `Forma.Framework.Localization`. It reads locale files against the runtime's own
rules, and every `IStringLocale` call site in C# and Razor against what the runtime will do with
it, and reports what would throw, render wrong, or quietly stay in English.

Two things go wrong quietly in a localized codebase, and a build catches neither:

- **A lookup throws, or renders the wrong thing.** `L["Stored {stored} of {total}.", 1]`
  supplies one value for two holes and throws at run time. A Hebrew translation that writes
  `{stord}` renders those seven characters literally, in front of a user.
- **A string silently stays in English.** Adding `L["Details columns"]` to a component whose `he`
  and `es` files already exist leaves both files behind, and falling back to the key is exactly
  what the runtime is defined to do, so nothing anywhere complains.

## Quick start

The binary is `locali`. The project is not packable, so run it from source:

```powershell
dotnet run --project src/Forma.Locali -- --path <target>
```

Lint a whole solution, skipping the test fixtures that are malformed on purpose:

```powershell
locali --path ../Forma/Forma.slnx --exclude "*/Fixtures/*"
```

Errors are reported by default; `--severity warning` reads the warnings as well. The examples
below use `locali` for the built binary.

## What it checks against

The linter never re-derives how a file is found: it runs the runtime's own resolution over the
project tree, so it places a file exactly where the application will. One type,
`Forma.Blazor.Components.Bars.FormaToolbar`, and the files that can localize it in `he`:

```text
src/Forma.Blazor/.locales/he.json                                   { "Components.Bars.FormaToolbar": { "Bold": "מודגש" } }
src/Forma.Blazor/Components/Bars/.locales/he.json                   { "FormaToolbar": { "Bold": "מודגש" } }
src/Forma.Blazor/Components/Bars/.locales/FormaToolbar.he.json      { "Bold": "מודגש" }
src/Forma.Blazor/Strings/.locales/he.json                           { "@.Components.Bars.FormaToolbar": { "Bold": "מודגש" } }
```

A folder is a namespace; a file is the folder's, named for the culture and sectioned by type, or
a type's own, named for the type; a section starting with `@.` names a type from the assembly's
root from anywhere. A host overrides any of them from its `.locales` folder beside the
application, laid out by the type's name, the file directly in its folder:

```text
.locales/Forma.Blazor/Components/Bars/he.json                       { "FormaToolbar": { "Bold": "עבה" } }
.locales/Forma.Blazor/Components/Bars/FormaToolbar.he.json          { "Bold": "עבה" }
```

The rules that read a type's files, LOC0112 and LOC0209 among them, read a project's own
embedded files, whatever folder they sit in; a host's folder translates other assemblies' types,
so the file rules read it and the type-bound rules do not, and LOC0118 reads it as a source of
its own. `Forma.Framework.Localization`'s README walks the whole layout; this document covers
the linter.

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

`--path` accepts a `.json` file, a `.cs` or `.razor` file, a `.csproj`, a `.sln`/`.slnx`, or a
directory. A directory is searched for `.json`, `.cs` and `.razor`, skipping `bin` and `obj`.
Source files are linted through the project that owns them, found by walking up to the nearest
`.csproj`.

`--exclude` is what makes a test tree lintable: a fixture that is malformed on purpose reports
what it exists to prove. A `.locali.json` file's `excludeFiles` entry does the same without a
flag on every invocation, and its `ignoreDiagnostics` entry leaves a named file out of named
rules; [Rules](docs/rules.md) describes both under the linting configuration.

`--severity` decides what counts as a finding. An error is something the runtime will get wrong;
a warning is something worth changing that still loads.

## Output

Console output is MSBuild-formatted, so an editor can navigate it:

```text
path(line,col): severity CODE: message
```

A diagnostic below `--severity`, or one the linting configuration leaves out, is left out of the
console, the JSON report and the exit code alike, but a run still briefs what it left out, so
nothing disappears without a trace:

```text
Linted 658 file(s); 6 diagnostic(s).

8 diagnostic(s) not shown:
  - LOC0106 (1)
  - LOC0205 (1), LOC0209 (4), LOC0210 (2)
Pass --severity warning to read them.
```

The brief names each rule and how many it contributed, a line per family. Its closing line only
promises what raising `--severity` will reveal: where the linting configuration accounts for
some or all of what is left out, it says so instead.

| Exit code | Meaning |
|-----------|---------|
| 0 | Every file linted clean at the reported severity |
| 1 | At least one diagnostic was reported |
| 2 | The command line was wrong, or a path could not be read |

`--report` writes a JSON report carrying `fileCount`, `diagnosticCount`, `suppressedCount`,
`ignoredCount` — how many of the suppressed diagnostics the linting configuration left out
rather than the severity — and a `diagnostics` array of `path`, `line`, `column`, `severity`,
`code`, `message`.

## How call sites are found

Call sites are found semantically where the compilation binds `IStringLocale`, and syntactically
otherwise. The syntactic path cannot resolve overloads, so the rules that count values stay
silent for it.

Almost every call site lives in `.razor`, which reaches Roslyn only through the Razor source
generator. The linter runs that generator itself, so nothing has to be emitted to disk and no
build property is needed; `locali --path MyProject.csproj` is enough. LOC0206 covers the case
where a project produced no generated Razor all the same. Loading Roslyn is itself a workaround,
described under [Contributing](#roslyn-is-loaded-from-the-sdk).

## Rules

[docs/rules.md](docs/rules.md) has an entry per rule: what it reports, why it matters at run
time, what fixes it, and where it stays silent on purpose. The linting configuration reports
under 100, a file rule in the 100s, and a call-site rule in the 200s.

| Code | Severity | Reports |
|------|----------|---------|
| LOC0020 | Error | The linting configuration is not readable JSON, does not follow its shape, or names a rule this linter does not define |
| LOC0021 | Warning | A glob in the linting configuration matched no file the run reached |
| LOC0022 | Error | The runtime's `.locale.json` is not readable, names a key the runtime does not read, gives a value it would refuse, is one of two for an assembly, or sits anywhere but its project's root |
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
| LOC0112 | Error | A locale file, or a section of one, names a type that does not exist |
| LOC0113 | Warning | The file translates the same key differently across its sections |
| LOC0115 | Warning | A reserved entry sits after an entry it has to precede |
| LOC0116 | Error | A key or section is shaped like a reserved entry the runtime does not define |
| LOC0117 | Error | The `$shared` entry is not an object holding translations alone |
| LOC0118 | Warning | Two files of one source stand for one anchor in one culture |
| LOC0119 | Warning | A folder of locale files is named for a type |
| LOC0201 | Error | An expression reads a name nothing supplies |
| LOC0202 | Error | The value count does not match the key's distinct holes |
| LOC0203 | Error | An expression naming no argument is given no value |
| LOC0204 | Error | A key carries a malformed expression |
| LOC0205 | Warning | A plural expression omits `other` |
| LOC0206 | Warning | A project's Razor markup went unchecked |
| LOC0208 | Error | A key writes text shaped like a hole the runtime cannot read |
| LOC0209 | Warning | A key is not answered in a culture its type is translated for |
| LOC0210 | Warning | A lookup writes its key as an expression |
| LOC0211 | Warning | A key carries holes but no words anywhere else |
| LOC0212 | Warning | An expression key carries no words in any field |
| LOC0213 | Error | A lookup writes its key as an interpolated string with holes |
| LOC0214 | Error | A lookup writes a key shaped like a reserved entry |
| LOC0215 | Error | A lookup supplies `string.Format`'s format text |

LOC0114, LOC0200 and LOC0207 are retired, and no number is reused.

## Contributing

### Layout

| Folder | Holds |
|--------|-------|
| `Cli/` | Argument parsing, options, exit codes, target resolution, the shared glob matcher |
| `Configuration/` | Reading `.locali.json` into the linting configuration a run applies, and `.locale.json` for the runtime's rule |
| `Diagnostics/` | `LintingCode`, the `LintingCodes` catalogue, `LintingDiagnostic` |
| `Documents/` | Reading a locale file into ordered entries with offsets |
| `Linting/` | The services that drive file and schema linting |
| `Projects/` | MSBuild loading, and binding Roslyn to the SDK's copy |
| `Reporting/` | Console and JSON reporters |
| `Rules/` | Rule contracts, rule contexts, shared hole-name reading |
| `Rules/Files/` | Rules over a locale file |
| `Rules/Sources/` | Rules over a call site |
| `Sources/` | Finding call sites, and the rules that read a project's locale files whole |

### Adding a rule

1. Add a `LintingCode` to `Diagnostics/LintingCodes.cs` and to its `All` list, numbered under
   100 for the linting configuration, in the 100s for a file rule, or in the 200s for a
   call-site rule.
2. Implement `ILocalizationFileRuleChecker` or `ICallSiteRuleChecker` under `Rules/Files/` or
   `Rules/Sources/`, or a linter over a project's files under `Sources/`. Expose the code
   through `Code` and report through the context, which owns the position.
3. Register it in `Program.cs`, and in the matching test factory under `tests/…/Support/`.
4. Write its entry in `docs/rules.md` and its row in the table above.

`LintCodesTests` fails if a rule reports under a code missing from the catalogue, if two codes
share an id, if two rules in the same family report under the same code, or if a rule reports
outside its family's range. The range rule means the same idea checked on both sides of a call
needs two codes, one per family, as LOC0110 and LOC0208 show. A rule that needs to report two
different things is two rules regardless of family.

`HoleNames` reads the names a format refers to its values by, and matches the runtime's
`LocalizedFormat.Names`. Use it rather than scanning for braces; a plural expression names its
argument in brackets, and escaped braces are literal text. A rule that depends on how values
reach the call site reads `context.Site.Arguments` and returns when it is `Unknown`.

### Build and test

```powershell
dotnet build Forma.Locali.slnx
dotnet test tests/Forma.Locali.Tests
dotnet format Forma.Locali.slnx --verify-no-changes
```

`SourceLinterTestFactory.Lint` compiles a class body against the real `IStringLocale` and
returns what the call-site rules report, so a rule is tested against a bound compilation rather
than a stub. Run a new rule over the sibling Forma repository before trusting it; it lints
clean, so anything a rule reports there is a false positive. It does not cover every shape a
call site can take — nothing there passes a values collection whole — so those belong in unit
tests.

### Roslyn is loaded from the SDK

This part is a workaround, and nothing above depends on understanding it.

Roslyn refuses to load a generator built against a newer compiler than the host, reporting
`ReferencesNewerCompiler` and returning no generators at all. The Razor generator ships inside
the SDK and is built against the SDK's Roslyn, which is ahead of anything published:

| | Version |
|---|---|
| Razor generator, from the SDK | built against Roslyn 5.9.0 |
| Newest `Microsoft.CodeAnalysis.CSharp` on nuget.org | 5.6.0 |

So the linter cannot load the generator using the packages it compiles against, and
`GetSourceGeneratedDocumentsAsync` returns nothing for Razor. What closes the gap is that the SDK
also carries a complete, version-matched Roslyn — compiler, workspaces and
`Workspaces.MSBuild` — under `DotnetTools`, shipped for `dotnet format` and `dotnet watch`.

`SdkRoslyn` binds to that copy from a `[ModuleInitializer]`, since resolution has to be
registered before the first Roslyn type loads, which is earlier than `Main`. The three
`Microsoft.CodeAnalysis.*` packages carry `ExcludeAssets="runtime"` so their assemblies stay
reference-only and cannot shadow the SDK's at run time. Redirecting only part of the stack does
not work: a 5.6.0 `Workspaces` on a 5.9.0 compiler throws `TypeLoadException` on members added
in the newer version. It is all or nothing.

When Roslyn cannot be bound, the linter stops with exit code 2 and explains what it looked for.
It cannot degrade: `ExcludeAssets="runtime"` keeps the published assemblies out of the output,
so if the SDK's are not found there is no Roslyn to run on at all. Failing at startup is what
keeps that from surfacing as an unrelated `FileNotFoundException` several frames later.

`DotnetTools` is an SDK implementation detail, not a documented redistribution point, so this
should not outlive the version gap. Once a Roslyn at or above the SDK's is on nuget.org, raise
the three `Microsoft.CodeAnalysis.*` versions, drop their `ExcludeAssets="runtime"`, delete
`Projects/SdkRoslyn.cs`, and register MSBuild in `MSBuildProjectLoader` again.
