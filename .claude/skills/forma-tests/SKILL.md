---
name: forma-tests
description: This repository's test standard — the xUnit v3 conventions (Should_ naming, Arrange/Act/Assert comments, one test class per production type), the Support/ factories that build documents, compilations, and linters, testing callsite rules against a real bound compilation through SourceLinterTestFactory, fixtures on disk through TempDirectory, and the registrations a new rule owes the test factories. Use when writing or changing any test, or adding a rule, factory, or fake.
---

# Tests (xUnit v3)

One test project, `tests/Forma.Locali.Tests`, tests the one production assembly. Its folders mirror the source, so `Rules/Files/DuplicateKeyChecker.cs` is tested by `tests/Forma.Locali.Tests/Rules/Files/DuplicateKeyCheckerTests.cs`, and the namespace follows the folder.

- Do name a test class `{Type}Tests`, `public sealed`, and name tests like `Should_report_a_key_the_file_declares_twice` — a sentence about behaviour, not a method name.
- Do mark the sections with explicit `// Arrange`, `// Act`, and `// Assert` comments.
- Do assert diagnostics by code through the `Codes()` extension, and assert position, message, or count only in a test that is about that.
- Do write one behaviour per test; a rule's silence cases ("Should_leave_… alone") are tests of their own, because silence is the rule working.
- Do test only the public surface, and keep shared usings in `_Imports.cs`.
- Don't couple tests to implementation details or private state.

## Support factories

Every fake, factory, and fixture lives under `tests/Forma.Locali.Tests/Support/`, in a folder mirroring the production area it serves; what no single area owns sits at the root, such as `TempDirectory`.

- Do build translation-file fixtures from `LocalizationFileFakeData` and read them through `LocalizationDocumentTestFactory` rather than inlining JSON, except where the malformation under test *is* the content.
- Do test a callsite rule through `SourceLinterTestFactory.Lint`, which compiles a class body against the real `IStringLocale` and runs the shipped checkers over the bound compilation; a rule tested against a stub proves nothing about how the semantic finder feeds it.
- Do test a project rule through `ProjectLinterTestFactory.Check`, which resolves a project's files as the linter does and runs the one rule over them, or over placed files read as the host's folder, with the locale packs of the run passed as `packs`; `LocalePackTestFactory.Resolve` builds one from files written under its `Folder`, named apart where a test needs a pack per culture.
- Do use `CompilationTestFactory.Unreferenced` for the syntactic path and `Referenced` for the semantic one.
- Do write on-disk fixtures through `TempDirectory`, which cleans up after the test.
- Do register a new checker in the matching test factory — `SchemaLinterTestFactory`, `SourceLinterTestFactory.Checkers()`, or `ProjectLinterTestFactory.Checkers()` — as well as in `Program.cs`; `LintCodesTests` enforces the catalogue, unique codes, and family ranges over whatever the factories return.
- Don't reach into the sibling Forma repository for test support; this project carries its own.

## What the unit suite cannot see

`SourceLinterTestFactory` compiles a scratch class, so nothing in it exercises MSBuild loading or the Razor source generator. The test host binds an older `NuGet.Frameworks` than MSBuild takes, so a project cannot be loaded inside it; `LocaliToolRunner` runs the built linter as a process of its own over a scratch project instead, which costs seconds a test, so it is kept for what only a real load can show. A change to `Projects/` or to how callsites are found wants a run of the real tool over the sibling Forma repository as well:

```powershell
dotnet run --project src/Forma.Locali -- --path ../Forma/Forma.slnx --exclude "*/Fixtures/*"
```

The tree lints clean, so anything a new rule reports there is a false positive.
