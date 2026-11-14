# Changelog

Each release of Locali, newest first. An entry is dated when its release is cut, and reads *Unreleased* until then.
The releases before `1.0.0-preview.11` are recorded by their [tags](https://github.com/FormaUI/Locali/tags) alone.

## `1.0.0-preview.13`

*Unreleased*

- Locale files are read as `Forma.Framework.Localization` now reads them. A file's culture is the first part of its
  name, folders included, that is a culture written exactly as .NET names it, so `.locales/es/Components.json` is a
  Spanish file, and a JSON file in a `.locales` folder, or whose file name ends in a culture, is read as a locale
  file, so LOC0101 reports one without `$localization`. An application, an executable or an assembly its build
  marks, reads its files for every assembly their names start with, and LOC0112 and LOC0120 hold such a file to
  the types of that assembly where a project of the run builds it. LOC0112 reports a library's file whose name starts
  with an assembly's as one only an application or a locale pack reads by that name.
- A locale pack's file is read for the longest assembly name its name starts with, the culture before the name or
  after it, rather than for its first folder. LOC0024 reports a pack file whose name starts with no assembly the
  solution builds or references, at the file.
- LOC0117 is retired, since two files of one source may stand for one name. LOC0120 reports, as an error, an entry
  a type reads that two files of one source give different texts, at the file whose name sorts first.
- LOC0027 warns where a part of the root namespace a project's locale files are embedded under, the assembly's name
  unless the project names another, is a culture, which the runtime reads as the culture of every file embedded
  under it.
- LOC0121 warns where an application's file has a name that reads both in the application's own namespace and as
  another assembly's, and a type of each reads one of its sections, so its entries answer both.
- LOC0122 warns where a part of a locale file's name is a culture written in another case than .NET names it, `ES`
  or `He`, and names no type, so the runtime reads it as part of the name rather than as the file's culture.

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
