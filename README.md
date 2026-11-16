# locali

A linter for `Forma.Framework.Localization`. It reads locale files against the runtime's own
rules, and every `IStringLocale` callsite in C#, Visual Basic and Razor against what the runtime
will do with it, and reports what would throw, render wrong, or quietly stay in the source
language.

Two things go wrong quietly in a localized codebase, and a build catches neither:

- **A lookup throws, or renders the wrong thing.** `L["Stored {stored} of {total}.", 1]`
  supplies one value for two holes and throws at run time. A Hebrew text that writes
  `{stord}` renders those seven characters literally, in front of a user.
- **A string silently stays in the source language.** Adding `L["Details columns"]` to a
  component whose `he` and `es` files already exist leaves both files behind, and falling back
  to the key is exactly what the runtime is defined to do, so nothing anywhere complains.

## Quick Start

The binary is `locali`, shipped on nuget.org as the dotnet tool `formaui-net.Locali`. A repository
pins it in its tool manifest and restores it, so every machine runs one version; a preview
installs only when it's asked for:

```powershell
dotnet new tool-manifest
dotnet tool install formaui-net.Locali --prerelease
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
src/Forma.Blazor/.locales/he/Components.Bars.json                   { "FormaToolbar": { "Bold": "מודגש" } }
src/Forma.Blazor/Components/Bars/.locales/FormaToolbar.he.json      { "Bold": "מודגש" }
src/Forma.Blazor/Strings/.locales/he.json                           { "@.Components.Bars.FormaToolbar": { "Bold": "מודגש" } }
```

A folder is a namespace; a file is the folder's, named for the culture and sectioned by type, or
a type's own, named for the type; a section starting with `@.` names a type from the assembly's
root from anywhere. A folder and a dot separate a name alike, the folder of locale files is left
out wherever it sits, `.locales` or, for every file a project embeds but a locale pack's, the
folder its `.locale.json` names as `localesDir`, and the culture is the first part of the name,
folders included, that is a culture written exactly as .NET names it, so the second and the
third file stand for one name. A source reads one entry per key: a type's own over a shared one,
and of two files giving one key different texts, the file whose name sorts last, which LOC0120
reports. An application overrides any of them in a file whose name starts with the assembly's
name, the culture before it or after it, which its build embeds as it does every locale file:

```text
.locales/Forma.Blazor/Components/Bars/he.json                       { "FormaToolbar": { "Bold": "עבה" } }
.locales/he/Forma.Blazor/Components.Bars.json                       { "FormaToolbar": { "Bold": "עבה" } }
```

The rules that read a type's files, LOC0112 and LOC0207 among them, read a project's own
embedded files, whatever folder they sit in, and LOC0207 reads a type of another project of the
run in that project's files. An application's file whose name starts with an assembly's name
localizes that assembly's types, so LOC0112 and LOC0120 hold it to that assembly's types where a
project of the run builds it, LOC0119 and LOC0207 read it for the application's own types where
that assembly is the application's own, and a host's file, one the project copies beside the
application rather than embeds, is read by the file rules alone. A locale pack, a project
declaring `IsLocalePack`, is an application's files for other assemblies shipped as an assembly:
each file's name starts with the name of the assembly it localizes, the culture before it or
after it, and goes on from the root namespace that assembly declares. The type-bound rules read
a pack's file for the project building the longest assembly name it starts with, so it is held
to that project's types and callsites as its own files are, LOC0120 reads each pack as a source
of its own, and LOC0024 reports a file whose name starts with no assembly the solution builds or
references. `Forma.Framework.Localization`'s README walks the whole layout; this document covers
the linter.

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
flag on every invocation, its `ignoreDiagnostics` entry leaves a named file out of named rules,
its `localeSources` entry says where each part of the tree keeps its entries, and its `hints`
entry tells the linter what it cannot read in the code, such as the member a compiled library's
locale is read for. A run reads one such file, the nearest at or above the path it was given;
[Rules](docs/linting-rules.md) describes all four entries under the linting configuration.

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
element access, a call named `Resolve` or a call to a method marked with a key, so a member
holding none is never bound; the syntactic path reads C# or Visual Basic syntax, cannot resolve
overloads, and so leaves the rules that count values silent.

A call to a method whose parameter carries `[LocaleKey]`, as `LocalizableText.For<TSource>`'s
key does, is a lookup on the semantic path: the argument for that parameter is its key, read in
the entries of the method's one type argument, and its values are counted where one `params`
parameter follows the key, as a lookup's are. A key a method passes on from a `[LocaleKey]`
parameter of its own is checked where that method is called.

A lookup's type is found on the semantic path by following its receiver back to where the
localization runtime decided it: a locale the factory the host registers creates for a type,
reached through `GetStringLocaleFactory()` or an injected `IStringLocaleFactory`, a value typed
`IStringLocale<T>`, followed further where a type of the run derives from `T`, since a locale
for it may stand behind one for `T`, or a `[LocaleKey]` method's type argument. The walk goes
back through locals assigned once, fields and properties as each object was given them,
parameters through every call the run makes to their method, constructors, lambdas, local
functions, delegates and lazy values, virtual calls on each type the instance can be, `base.`
calls to the member they name, and services to the one implementation the run's product code
registers, so `L["Details columns"]` in a component is held to the component's own files however
its base reaches the factory. Where the walk cannot follow a value, such as a type chosen at run
time, a factory the code builds itself, or a service registered in a way the walk cannot read,
the path ends and the lookup is held to no type along it; alternatives the walk cannot pair with
the lookup, such as both sides of a conditional, whether locales, objects holding them, or
delegates returning them, are never held to it for certain.

What the walk cannot read, a hint states: a member whose locale is read for the type another
member of the same instance returns, as `FormaComponentBase.L` is for `LocaleSourceType`. The
walk still runs first, without hints, and a hint is verified where it reaches the same type, set
aside and reported where it reaches another, and used where it reaches none, as over a compiled
library, where the type member's getter is read in the compiled code of the assembly the build
runs. An application writes its own hints in `.locali.json`; a library keeps its hints in a
`.locali-hints.json` at its project's root, which a run building the library reads and holds to
its code, and ships it at its package's root, where an application reads it by naming the
package. Nothing is written into the code.

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
| LOC0024 | Error | A locale pack's file has a name starting with no assembly the solution builds or references |
| LOC0025 | Warning | A locale file comes from a source its `localeSources` line does not allow, a pack's file held to the line over the project it localizes |
| LOC0026 | Warning | A `.locali.json` sits beneath the path the run was given, other than the one the run reads |
| LOC0027 | Warning | A part of the root namespace a project's locale files are embedded under, the assembly's name unless the project names another, is a culture |
| LOC0028 | Error | A hint names a type, a member or a package that is not there or not of the kind the hint reads |
| LOC0029 | Error | A hint disagrees with another written for the same member, or with the type the flow reaches on its own |
| LOC0100 | Error | The file is not readable JSON |
| LOC0101 | Error | The file carries no `$localization` entry |
| LOC0102 | Warning | The metadata entry is not the file's first property |
| LOC0103 | Error | The metadata entry does not follow the field grammar |
| LOC0104 | Error | The metadata entry omits a required field |
| LOC0105 | Warning | The metadata entry declares its required fields out of order |
| LOC0106 | Error | The file is written for a schema this build does not define |
| LOC0107 | Warning | The metadata entry declares an unknown field |
| LOC0108 | Error | The file declares a culture its own name does not give it |
| LOC0109 | Error | A text fills a hole its key does not name |
| LOC0110 | Error | A text writes something shaped like a hole the runtime cannot read |
| LOC0111 | Error | The file declares the same key more than once |
| LOC0112 | Error | A locale file, or a section of one, names a type that does not exist |
| LOC0113 | Warning | The file gives the same key different texts across its sections |
| LOC0114 | Warning | A reserved entry sits after an entry it has to precede |
| LOC0115 | Error | A key or section is shaped like a reserved entry the runtime does not define |
| LOC0116 | Error | The `$shared` entry is not an object holding entries alone |
| LOC0118 | Warning | A folder of locale files is named for a type |
| LOC0119 | Warning | A type is localized in one culture family the project's files or packs carry and left out of another |
| LOC0120 | Error | Two files of one source give an entry a type reads different texts |
| LOC0121 | Warning | An application's file has a name that reads both in its own namespace and as another assembly's, and a type of each reads one of its sections |
| LOC0122 | Warning | A part of a locale file's name is a culture written in another case than .NET names it, and names no type |
| LOC0123 | Warning | A part of a locale file's name is a culture other than the file's own, and names no type |
| LOC0124 | Error | A satellite assembly holds a locale file under a name whose culture is not the satellite's |
| LOC0200 | Error | An expression reads a name nothing supplies |
| LOC0201 | Error | The value count does not match the key's distinct holes |
| LOC0202 | Error | An expression naming no argument is given no value |
| LOC0203 | Error | A key carries a malformed expression |
| LOC0204 | Warning | A plural expression omits `other` |
| LOC0205 | Warning | A project's Razor markup went unchecked |
| LOC0206 | Error | A key writes text shaped like a hole the runtime cannot read |
| LOC0207 | Warning | A key is not answered in a culture its type is localized in |
| LOC0208 | Warning | A lookup writes its key as an expression |
| LOC0209 | Warning | A key carries holes but no words anywhere else |
| LOC0210 | Warning | An expression key carries no words in any field |
| LOC0211 | Error | A lookup writes its key as an interpolated string with holes |
| LOC0212 | Error | A lookup writes a key shaped like a reserved entry |
| LOC0213 | Error | A lookup supplies `string.Format`'s format text |
| LOC0214 | Warning | A lookup's type through a hinted member could not be read |

## Contributing

### Layout

The engine, `src/Forma.Locali.Engine`, holds everything a run does and registers it through
`AddLocaliEngine`; the tool, `src/Forma.Locali`, is the `locali` command around it. Both have the
root namespace `Forma.Locali`, so a folder's namespace is the same in either project.

| Project | Folder | Holds |
|---------|--------|-------|
| Engine | `Cli/` | Target resolution, the options a run takes, the shared glob matcher |
| Engine | `Configuration/` | Reading `.locali.json` into the linting configuration a run applies, with the hints it states and imports, and `.locale.json` for the runtime's rule |
| Engine | `Diagnostics/` | `LintingCode`, the `LintingCodes` catalogue, `LintingDiagnostic` |
| Engine | `Documents/` | The locale format, `ILocaleFormat`, and its JSON implementation, which reads a locale file into ordered entries with offsets |
| Engine | `Linting/` | The services that drive file and schema linting |
| Engine | `Projects/` | MSBuild loading |
| Engine | `Rules/` | Rule contracts, rule contexts, shared hole-name reading |
| Engine | `Rules/Files/` | Rules over a locale file |
| Engine | `Rules/Sources/` | Rules over a callsite |
| Engine | `Rules/Projects/` | Rules over a project's locale files read whole |
| Engine | `Flow/` | Following a lookup's locale back to where the runtime decided its type, across the projects of a run, and holding hints to it |
| Engine | `Sources/` | Finding callsites, and resolving a project's locale files, the locale packs carrying files for it, and the assemblies of the run an application's files are read for, for the project rules |
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
show. `build.ps1` runs the build, both test projects and the format check, the fast gate, and `-Clean` removes
every `bin`, `obj` and `artifacts` folder and `.sandbox/tmp` first so they start from nothing.
`build.ps1 -Full` runs the full gate: the fast gate, then it packs the tool and verifies the package, as
[Versioning](#versioning) sets out.

Work that belongs to a person rather than to the repository, such as a benchmark, an experiment or a scratch file,
goes under `.sandbox/`, which git ignores whole. A run's temporary folders live in `.sandbox/tmp/`: the tests write
their files there, and `-Full` installs the package there, each under build files of its own that MSBuild and the
compiler read before the repository's, so a project there loads as it would outside the repository.
`Forma.Locali.code-workspace` keeps the editor from watching `.sandbox/tmp/`, so it loads none of those projects
while a run is still using or removing them.

`SourceLinterTestFactory.Lint` compiles a class body against the real `IStringLocale` and
returns what the callsite rules report, so a rule is tested against a bound compilation rather
than a stub. Run a new rule over the sibling Forma repository before trusting it; it lints
clean, so anything a rule reports there is a false positive. It does not cover every shape a
callsite can take — nothing there passes a values collection whole — so those belong in unit
tests.

### Versioning

Locali follows [FormaUI/Versioning `v1.0.0-preview.1`](https://github.com/FormaUI/Versioning/blob/v1.0.0-preview.1/README.md), which sets out how
it versions, branches, commits, releases and packages its work, and leaves Locali to state:

- **Its public contract** is the command line: the [options](#options), the console output's format
  and the exit codes under [Output](#output), the JSON report `--report` writes, and the rule ids in
  the [rules](#rules) tables, each with what it reports and its severity.
- **Its version** is declared once, by `Version` in
  [`src/Forma.Locali/Forma.Locali.csproj`](src/Forma.Locali/Forma.Locali.csproj), and the package,
  the release's tag and the [changelog](CHANGELOG.md) repeat it.
- **Its fast gate** is `./build.ps1`, which `ci.yml` runs on every push but `main`'s and on every
  pull request, with a lint of the sibling Forma's `dev`. **Its full gate** is `./build.ps1 -Full`,
  which packs the tool, checks the package against its inventory, installs it from a local feed
  into a consumer of its own and runs it over a sample project; `full.yml` runs it on every push to
  `candidate`, with the fast gate on Windows and the lint of Forma, and keeps the verified package
  as the artifact `packages`.
- **Its package** is `formaui-net.Locali` on nuget.org, a .NET tool for `net11.0` whose command is
  `locali`, depending on no package. It holds the files
  [`package-files.txt`](src/Forma.Locali/package-files.txt) lists, with its symbols beside it as a
  `.snupkg`; the SDK builds a tool's symbol package from the `.pdb` files the tool holds, so those
  stay in the package as well.
- **Its release** is published by `release.yml`, as [Releasing](#releasing) sets out.
- **Its changelog** is [`CHANGELOG.md`](CHANGELOG.md).

A commit's message follows Versioning's [commits](https://github.com/FormaUI/Versioning/blob/v1.0.0-preview.1/docs/commits.md).

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
once every project is loaded, since a locale pack's files resolve against the whole run. A
referenced project is evaluated only where the assembly it builds is missing, in a tree that
was never built, and its compilation stands in for the assembly.

MSBuild itself is loaded from the installed SDK through `MSBuildLocator`, so the `Microsoft.Build`
packages stay reference-only too.

## License

Locali is licensed under the [Apache License 2.0](LICENSE). This supersedes any license an
earlier release was published under: every release is available under the Apache License 2.0.
