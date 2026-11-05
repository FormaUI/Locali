# locali

A linter for `Forma.Framework.Localization`. It reads locale files against the runtime's own
rules, and every `IStringLocale` callsite in C#, Visual Basic and Razor against what the runtime
will do with it, and reports what would throw, render wrong, or quietly stay in English.

Two things go wrong quietly in a localized codebase, and a build catches neither:

- **A lookup throws, or renders the wrong thing.** `L["Stored {stored} of {total}.", 1]`
  supplies one value for two holes and throws at run time. A Hebrew translation that writes
  `{stord}` renders those seven characters literally, in front of a user.
- **A string silently stays in English.** Adding `L["Details columns"]` to a component whose `he`
  and `es` files already exist leaves both files behind, and falling back to the key is exactly
  what the runtime is defined to do, so nothing anywhere complains.

## Quick Start

The binary is `locali`, shipped as the dotnet tool `formaui-net.Locali`. A repository pins it in its tool
manifest and restores it, so every machine runs one version:

```powershell
dotnet new tool-manifest
dotnet tool install formaui-net.Locali
dotnet locali --path <target>
```

To run it from source instead:

```powershell
dotnet run --project src/Forma.Locali -- --path <target>
```

Lint a whole solution, skipping the test fixtures that are malformed on purpose:

```powershell
locali --path ../formaui-net/Forma.slnx --exclude "*/Fixtures/*"
```

Errors are reported by default; `--severity warning` reads the warnings as well. The examples
below use `locali` for the built binary.

## What It Checks Against

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
root from anywhere. A host overrides any of them from the `.locales` folder at its root, a folder
for each assembly laid out by the type's name, the file directly in its folder, which an
application's build embeds and a project file may copy beside the application instead:

```text
.locales/Forma.Blazor/Components/Bars/he.json                       { "FormaToolbar": { "Bold": "עבה" } }
.locales/Forma.Blazor/Components/Bars/FormaToolbar.he.json          { "Bold": "עבה" }
```

The rules that read a type's files, LOC0112 and LOC0207 among them, read a project's own
embedded files, whatever folder they sit in; a host's folder translates other assemblies' types,
so the file rules read it and the type-bound rules do not, whether its files are embedded or
copied, and LOC0117 reads its embedded files and its copied ones as two sources, as the runtime
does. A locale pack, a project declaring `IsLocalePack`, is a host's folder that ships as an
assembly: its top-level folders name the assemblies it localizes, each laid out by the root
namespace that assembly declares. The type-bound rules read a pack's folder for the project it
names, so a pack's files are held to that project's types and callsites as its own files are,
LOC0117 reads each pack as a source of its own, and LOC0024 reports a folder that names no
assembly the solution builds or references. `Forma.Framework.Localization`'s README walks the
whole layout; this document covers the linter.

## Options

```text
Usage: locali --path <file-or-directory> [--report [<path>]] [--exclude <glob>]
              [--severity <level>] [--property <name>=<value>]

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
  --property An MSBuild property every project is loaded with, written as
             'Configuration=Release'. May be given more than once. A project
             loads as its default build otherwise, so what it compiles or
             embeds only under another configuration goes unseen.
```

`--path` accepts a `.json` file, a `.cs`, `.vb` or `.razor` file, a `.csproj` or `.vbproj`, a
`.sln`/`.slnx`, or a directory. A directory is searched for `.json`, `.cs`, `.vb` and `.razor`,
skipping `bin` and `obj`. Source files are linted through the project that owns them, found by
walking up to the nearest `.csproj` or `.vbproj`.

`--exclude` is what makes a test tree lintable: a fixture that is malformed on purpose reports
what it exists to prove. A `.locali.json` file's `excludeFiles` entry does the same without a
flag on every invocation, its `ignoreDiagnostics` entry leaves a named file out of named
rules, and its `localeSources` entry says where each part of the tree keeps its translations.
A run reads one such file, the nearest at or above the path it was given; [Rules](docs/linting-rules.md)
describes all three entries under the linting configuration.

`--severity` decides what counts as a finding. An error is something the runtime will get wrong;
a warning is something worth changing that still loads.

`--property` names the build being linted. A project is read as MSBuild evaluates it, and
without a property that is its default build, so a locale file embedded only under `Release`
reads as one the project does not embed. `--property Configuration=Release` loads the
compilation and the embedded files alike under that build.

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
  - LOC0204 (1), LOC0207 (4), LOC0208 (2)
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

## How Callsites Are Found

Callsites are found semantically where the compilation binds `IStringLocale`, and syntactically
otherwise. The semantic path reads the operation tree the compiler binds, which has one shape
for every language it compiles, and asks it only about the nodes a lookup can be written as, an
element access or a call named `Resolve`, so a member holding neither is never bound; the
syntactic path reads C# or Visual Basic syntax, cannot resolve overloads, and so leaves the rules
that count values silent.

Almost every callsite lives in `.razor`, which reaches Roslyn only through the Razor source
generator. The linter runs that generator itself, so nothing has to be emitted to disk and no
build property is needed; `locali --path MyProject.csproj` is enough. LOC0205 covers the case
where a project produced no generated Razor all the same. The linter runs on the
SDK's own compiler so that the generator always loads, described under
[Contributing](#roslyn-is-the-sdks-compiler).

## Rules

[docs/linting-rules.md](docs/linting-rules.md) has an entry per rule: what it reports, why it matters at run
time, what fixes it, and where it stays silent on purpose. The linting configuration reports
under 100, a file rule in the 100s, and a callsite rule in the 200s.

| Code | Severity | Reports |
|------|----------|---------|
| LOC0020 | Error | The linting configuration is not readable JSON, does not follow its shape, or names a rule this linter does not define |
| LOC0021 | Warning | An entry in the linting configuration applied to no file the run reached |
| LOC0022 | Error | The runtime's `.locale.json` is not readable, names a key the runtime does not read, gives a value it would refuse, is one of two for an assembly, sits anywhere but its project's root, or sits in a locale pack |
| LOC0023 | Warning | The root namespace the runtime counts a type's name from holds none of the project's types |
| LOC0024 | Error | A locale pack's folder names an assembly the solution neither builds nor references |
| LOC0025 | Warning | A locale file comes from a source its `localeSources` line does not allow, a pack's file held to the line over the project it localizes |
| LOC0026 | Warning | A `.locali.json` sits beneath the path the run was given, other than the one the run reads |
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
| LOC0114 | Warning | A reserved entry sits after an entry it has to precede |
| LOC0115 | Error | A key or section is shaped like a reserved entry the runtime does not define |
| LOC0116 | Error | The `$shared` entry is not an object holding translations alone |
| LOC0117 | Warning | Two files of one source stand for one anchor in one culture |
| LOC0118 | Warning | A folder of locale files is named for a type |
| LOC0119 | Warning | A type is translated in one culture family the project's files or packs carry and left out of another |
| LOC0200 | Error | An expression reads a name nothing supplies |
| LOC0201 | Error | The value count does not match the key's distinct holes |
| LOC0202 | Error | An expression naming no argument is given no value |
| LOC0203 | Error | A key carries a malformed expression |
| LOC0204 | Warning | A plural expression omits `other` |
| LOC0205 | Warning | A project's Razor markup went unchecked |
| LOC0206 | Error | A key writes text shaped like a hole the runtime cannot read |
| LOC0207 | Warning | A key is not answered in a culture its type is translated for |
| LOC0208 | Warning | A lookup writes its key as an expression |
| LOC0209 | Warning | A key carries holes but no words anywhere else |
| LOC0210 | Warning | An expression key carries no words in any field |
| LOC0211 | Error | A lookup writes its key as an interpolated string with holes |
| LOC0212 | Error | A lookup writes a key shaped like a reserved entry |
| LOC0213 | Error | A lookup supplies `string.Format`'s format text |

## Contributing

### Layout

The engine, `src/Forma.Locali.Engine`, holds everything a run does and registers it through
`AddLocaliEngine`; the tool, `src/Forma.Locali`, is the `locali` command around it. Both have the
root namespace `Forma.Locali`, so a folder's namespace is the same in either project.

| Project | Folder | Holds |
|---------|--------|-------|
| Engine | `Cli/` | Target resolution, the options a run takes, the shared glob matcher |
| Engine | `Configuration/` | Reading `.locali.json` into the linting configuration a run applies, and `.locale.json` for the runtime's rule |
| Engine | `Diagnostics/` | `LintingCode`, the `LintingCodes` catalogue, `LintingDiagnostic` |
| Engine | `Documents/` | The locale format, `ILocaleFormat`, and its JSON implementation, which reads a locale file into ordered entries with offsets |
| Engine | `Linting/` | The services that drive file and schema linting |
| Engine | `Projects/` | MSBuild loading |
| Engine | `Rules/` | Rule contracts, rule contexts, shared hole-name reading |
| Engine | `Rules/Files/` | Rules over a locale file |
| Engine | `Rules/Sources/` | Rules over a callsite |
| Engine | `Rules/Projects/` | Rules over a project's locale files read whole |
| Engine | `Sources/` | Finding callsites, and resolving a project's locale files, and the locale packs carrying files for it, for the project rules |
| Tool | `Cli/` | Argument parsing and exit codes |
| Tool | `Projects/` | Binding Roslyn to the SDK's copy |
| Tool | `Reporting/` | Console and JSON reporters |

### Adding a Rule

1. Add a `LintingCode` to `Diagnostics/LintingCodes.cs` and to its `All` list, numbered under
   100 for the linting configuration, in the 100s for a file rule, or in the 200s for a
   callsite rule; a project rule is numbered for what it reports about, a config file, a locale
   file, or a callsite.
2. Implement `ILocalizationFileRuleChecker`, `ICallsiteRuleChecker`, or `IProjectRuleChecker`
   under `Rules/Files/`, `Rules/Sources/`, or `Rules/Projects/`. Expose the code through `Code`
   and report through the context, which owns the position.
3. Register it in `AddLocaliEngine`, and in the matching test factory under
   `tests/Forma.Locali.Engine.Tests/Support/`.
4. Write its entry in `docs/linting-rules.md` and its row in the table above.

`LintCodesTests` fails if a rule reports under a code missing from the catalogue, if two codes
share an id, if two rules in the same family report under the same code, or if a file or
callsite rule reports outside its family's range. The range rule means the same idea checked on
both sides of a call needs two codes, one per family, as LOC0110 and LOC0206 show. A rule that
needs to report two different things is two rules regardless of family.

`HoleNames` reads the names a format refers to its values by, and matches the runtime's
`LocalizedFormat.Names`. Use it rather than scanning for braces; a plural expression names its
argument in brackets, and escaped braces are literal text. A rule that depends on how values
reach the callsite reads `context.Site.Arguments` and returns when it is `Unknown`.

### Build and Test

```powershell
dotnet build Forma.Locali.slnx
dotnet test tests/Forma.Locali.Engine.Tests
dotnet test tests/Forma.Locali.Tests
dotnet format Forma.Locali.slnx --verify-no-changes
```

`tests/Forma.Locali.Engine.Tests` tests the engine, and `tests/Forma.Locali.Tests` tests the
tool: its command line, its reporters, and the project loads only a run of the built tool can
show. `build.ps1` runs the build, both test projects and the format check, and `-Clean` removes every `bin`,
`obj` and `artifacts` folder first so they start from nothing.

`SourceLinterTestFactory.Lint` compiles a class body against the real `IStringLocale` and
returns what the callsite rules report, so a rule is tested against a bound compilation rather
than a stub. Run a new rule over the sibling Forma repository before trusting it; it lints
clean, so anything a rule reports there is a false positive. It does not cover every shape a
callsite can take — nothing there passes a values collection whole — so those belong in unit
tests.

### Releasing

A release follows Versioning's
[releases](https://github.com/FormaUI/Versioning/blob/v1.0.0-preview.1/docs/releases.md): the
version and the changelog entry's date are set on `dev`, `candidate` fast-forwards to `dev`'s tip
so `full.yml` runs on it, a pull request from `candidate` into `main` passes the promotion check
and `main` fast-forwards to it, and a release signer tags `main`:

```powershell
git tag -s v1.0.0-preview.11 -m "Locali 1.0.0-preview.11"
git push origin v1.0.0-preview.11
```

The tag's push runs `release.yml`, which holds the tag to the release check, attaches the package
`full.yml` verified to the tag's GitHub release with its digest, and publishes it to nuget.org
through trusted publishing, as `soyeyalon`, the account whose policy names this repository and
`release.yml`. A failed publication runs again for the same tag.

### Roslyn Is The SDK's Compiler

The Razor generator ships inside the SDK, built against the SDK's own Roslyn, and Roslyn refuses
to load a generator built against a newer compiler than its host: it reports
`ReferencesNewerCompiler` and returns no generators at all. The linter therefore ships no Roslyn.
It runs on the compiler of the SDK it finds, the one every generator of that SDK is built
against, so a newer SDK needs nothing changed here.

`SdkCompiler` binds to `Roslyn/bincore` under the SDK, the folder `csc` and `vbc` run from, in a
`[ModuleInitializer]`, since resolution has to be registered before the first Roslyn type loads,
which is earlier than `Main`. The engine's `Microsoft.CodeAnalysis.CSharp` and
`Microsoft.CodeAnalysis.VisualBasic` packages carry `ExcludeAssets="runtime"` so they stay
reference-only, and `PrivateAssets="all"` so they reach neither the tool nor anything else that
references the engine, and their version is the oldest Roslyn API the linter is written against: a
newer compiler runs code built against an older one, as it does for every analyzer. When no
compiler is found the linter stops with exit code 2 and says where it looked.

A project is loaded in one MSBuild design-time build, which runs the project's own targets up to
the compiler and hands back what it would have been given, together with the names the build
embeds each resource under. `CompilerCommandLine` turns those arguments into a compilation and
runs the generators they name, as the compiler would. The targets run in one build because a
second build of the same project instance fails, and every project of a run builds inside one
MSBuild session in one project collection, so the SDK's targets are parsed once and what MSBuild
resolves for one project, its SDKs and assembly references among it, serves the next. MSBuild
builds one project after another on a thread of its own, the compiler turns each built project
into a compilation on the cores beside it, several at a time, and a project's callsites are
linted as soon as its compilation lands, in whatever order they land; the project rules run
once every project is loaded, since a locale pack's folders resolve against the whole run. A
referenced project is evaluated only where the assembly it builds is missing, in a tree that
was never built, and its compilation stands in for the assembly.

MSBuild itself is loaded from the installed SDK through `MSBuildLocator`, so the `Microsoft.Build`
packages stay reference-only too.

## License

Locali is licensed under the [Apache License 2.0](LICENSE). This supersedes any license an
earlier release was published under: every release is available under the Apache License 2.0.
