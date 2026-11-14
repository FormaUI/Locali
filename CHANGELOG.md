# Changelog

Each release of Locali, newest first. An entry is dated when its release is cut, and reads *Unreleased* until then.
The releases before `1.0.0-preview.11` are recorded by their [tags](https://github.com/FormaUI/Locali/tags) alone.

## `1.0.0-preview.13`

*Unreleased*

- Locale files are read as `Forma.Framework.Localization` now reads them. A file's culture is the first part of its
  name, folders included, that is a culture written exactly as .NET names it, so `.locales/es/Components.json` is a
  Spanish file, and a JSON file in a `.locales` folder, or whose file name ends in a culture, is read as a locale
  file, so LOC0101 reports one without `$localization`. An application, an executable or an assembly its build
  marks, reads its files for every assembly their names start with, and LOC0112 leaves such a file to that
  assembly.
- A locale pack's file is read for the longest assembly name its name starts with, the culture before the name or
  after it, rather than for its first folder. LOC0024 reports a pack file whose name starts with no assembly the
  solution builds or references, at the file.

## `1.0.0-preview.12`

*November 14, 2026*

- A call to a method whose parameter carries `[LocaleKey]`, as `LocalizableText.For<TSource>`'s key does, is read
  as a lookup: its key is checked as a lookup's is, against the translations of the method's type argument, and a
  key passed on from a parameter carrying `[LocaleKey]` is checked where its own method is called.

## `1.0.0-preview.11`

*November 5, 2026*

- The package links each of its assemblies to the commit it was built from through Source Link, carries its symbols
  in a `.snupkg` beside it, and names this entry as its release notes.
- The package is published to nuget.org alone; the FormaUI organization's GitHub Packages feed receives no new
  version.
