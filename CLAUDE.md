# Forma.Locali

A standalone CLI localization linter. **.NET 11 · C# latest · console tool · binary name `locali`.**

The linter reads locale files against Forma's localization schema, and reads every `IStringLocale` callsite in C# and Razor against what the runtime will do with it. It was extracted from the Forma repository and lives beside it; Forma remains both the source of the runtime it mirrors and its primary lint target.

## Task guides

Detailed standards live in `.claude/skills/` and load on demand. Read the matching guide before starting that kind of work — each is authoritative for its area.

| Skill | Read it before |
|---|---|
| `forma-localization` | Touching anything that reads or writes the localization format — a rule, a fixture, a schema type |
| `forma-tests` | Writing or changing any test, or adding a test factory or fake |

## Source of truth

- Do use [README.md](README.md) for everything about the tool itself: the CLI, every rule and where it stays silent, the source layout, how to add a rule, and how the linter runs on the SDK's compiler. It is the authority; this file only says what the repository around it expects.
- Do read `.editorconfig` before writing C#; it is the authority on style, and the root file and `tests/.editorconfig` differ deliberately.
- Do treat the sibling **Forma** repository (`c:\Users\eyal\Development\Projects\Forma`) as the reference for how the runtime behaves; a rule is right when it reports exactly what `Forma.Framework.Localization` will get wrong at run time.
- Don't invent scope, status, or style rules when an authoritative file defines them.

## Solution structure

Two projects, one solution:

```text
Forma.Locali.slnx
├── src/Forma.Locali            # the linter; assembly name `locali`, namespace Forma.Locali
└── tests/Forma.Locali.Tests    # its only test project
```

- Do keep the dependency on the runtime as cross-repository `ProjectReference`s into `..\Forma\src\Forma.Framework\Forma.Framework.Localization`, whose file layout and parsers the rules call, and `..\Forma\src\Forma.Framework\Forma.Framework.Localization.Json`, whose format reads the files. The Forma repository must sit beside this one for the solution to build.
- Do compile against the real runtime rather than re-implementing any of its parsing here; the linter's promise is that a rule and the runtime cannot disagree, and it keeps that promise by calling the runtime's own parser, plural rules, and schema types.
- Do keep a folder and its namespace in step, so `src/Forma.Locali/Rules/Files/` holds `Forma.Locali.Rules.Files`.
- Do reference the narrowest thing that supplies what the code uses, give every project its own reference for what it uses directly, and keep no reference no code uses.
- Don't add a Roslyn package with runtime assets, such as `Microsoft.CodeAnalysis.Workspaces`, or raise `Microsoft.CodeAnalysis.CSharp` beyond the API the code needs: the linter runs on the installed SDK's compiler, bound by `SdkCompiler`, and the README's *Roslyn Is The SDK's Compiler* section says why. Do leave `ExcludeAssets="runtime"` on it and on the `Microsoft.Build` references.

## Rules

- Do treat a false positive as the worst defect a rule can have: the linter runs in build gates, so a rule fires only where the failure is certain, and the README's *Where a rule stays silent* section is binding.
- Do follow the README's *Adding a rule* steps exactly — code in the catalogue and its `All` list, checker under `Rules/Files/`, `Rules/Sources/`, or `Rules/Projects/`, registration in `Program.cs`, registration in the matching test factory — because `LintCodesTests` enforces the catalogue and the family ranges.
- Do run a new rule over the sibling Forma repository's `src` before trusting it; the tree lints clean, so anything a new rule reports there is a false positive.
- Do write a diagnostic message as one sentence saying what is wrong and what the runtime does about it, matching the voice of the existing messages.
- Don't widen an existing rule and its message in one change without re-reading the README's table row for it; the row states what the rule reports and drifts silently.

## Comments and documentation

These rules govern prose that *describes a thing*. They do not apply to this file.

- Do state what a thing is in XML docs and README prose.
- Do explain how something works only where the mechanism is intricate enough that a reader could not recover it from the code, and then describe the mechanism rather than the reasoning that chose it.
- Do use US English spelling in documentation and comments — color, license, catalog — with one deliberate exception: behaviour keeps its British spelling.
- Do keep comments to one line whenever possible.
- Do add a one-line XML `<summary>` to every public type and member.
- Do put `/// <inheritdoc />` on every interface implementation rather than restating what the contract already documents.
- Do re-read the prose around whatever you touch; a description is wrong the moment the thing it describes widens, and nothing in the build will say so. The README's rule tables and layout table are where this repository drifts.
- Don't document history, rejected alternatives, or roadmap position.
- Don't write what a thing might do, does not do, or would otherwise have done.
- Don't restate obvious code; delete a comment that carries nothing rather than rewording it.

## Naming

- Do name a field, local, or parameter for the role its value plays rather than for its type or its mechanism, and spell it out; abbreviations and single letters belong to loop counters alone.
- Do name a checker `{WhatItChecks}Checker` and its file after it, reporting one `LintCode`; a rule that needs to report two different things is two rules.
- Do suffix asynchronous methods with `Async`.
- Don't leave a name that has drifted from what it holds; a rename is cheap and the compiler proves it.

## Build and verification

```powershell
dotnet build Forma.Locali.slnx
dotnet test tests/Forma.Locali.Tests
dotnet format Forma.Locali.slnx --verify-no-changes
```

No project sets `GenerateDocumentationFile`, so nothing reports an undocumented public member. Turn it on for one throwaway build to list them:

```powershell
dotnet build Forma.Locali.slnx -p:GenerateDocumentationFile=true --no-incremental -v:q -nologo |
    Select-String CS1591
```

- Do require build, tests, and the format check to pass before offering work for review.
- Do run the `CS1591` sweep after adding or changing public API, and ignore the hits under `tests/`, which are test method names.
- Do run the format check rather than trusting the build; it catches using-directive ordering and line endings that compilation ignores.
- Do finish by running the tool itself over the sibling Forma repository when a change touches what a rule reports:

  ```powershell
  dotnet run --project src/Forma.Locali -- --path ../Forma/Forma.slnx --exclude "*/Fixtures/*"
  ```

## Working agreement

- Do build, review, and hand over one change at a time — one rule, one fix, one refactor.
- Do keep changes minimal and on-scope.
- Do finish a piece of work with it unstaged, report what changed and what the gates said, and propose a commit message for the maintainer to run themselves.
- Don't run a state-changing Git command — `add`, `commit`, `push`, `reset`, `restore`, `stash`, a path-scoped `checkout` — without permission for that command, asked for once the work exists and the maintainer can see what is in it. An instruction given up front about work not yet done grants nothing, and neither does permission for an earlier commit; read-only Git needs no permission at all.
- Don't retrofit new conventions onto existing code unless asked.
