# Changelog

Each release of Locali, newest first. An entry is dated when its release is cut, and reads *Unreleased* until then.
The releases before `1.0.0-preview.11` are recorded by their [tags](https://github.com/FormaUI/Locali/tags) alone.

## `1.0.0-preview.16`

*Unreleased*

- LOC0025, LOC0101 and LOC0108 read a file in a project without the folder of locale files its `.locale.json`
  names as `localesDir`, as the build embeds it and `Forma.Framework.Localization` reads it, rather than without
  `.locales` alone, so a file in that folder is held to the culture its name gives it and to the sources
  `localeSources` allows. A file in the host's `.locales` folder at the project's root is read without `.locales`,
  as the host reads it.

## `1.0.0-preview.15`

*November 15, 2026*

- An application's files for other assemblies are read without the folder of locale files its own `.locale.json`
  names as `localesDir`, as `Forma.Framework.Localization` reads them, rather than without `.locales`, so LOC0112,
  LOC0120 and LOC0121 hold such a file to the other assembly's types. A locale pack's files are read without
  `.locales`, whatever its project's `.locale.json` names.

## `1.0.0-preview.14`

*November 15, 2026*

- A project's locale files are read without the folder of locale files its `.locale.json` names as `localesDir`, as
  `Forma.Framework.Localization` reads them, rather than without `.locales` alone. Where `localesDir` names another
  folder, the rules that read a type's files, LOC0027 and LOC0122 among them, now read the project's files by the
  names the runtime reads them by: a folder starting with a dot used to leave every file unread, and any other was
  read into the name. A project whose items were not read is taken to embed the files in that folder and the
  `.locale.json` at its root, as the build does.
- LOC0027 and LOC0122 read a name a file is embedded under only where it ends in the format's extension, as the
  runtime does, so a file a project embeds under a name of its own is not read for a culture it holds, and a name
  with no extension no longer ends the run with an error.
- LOC0122's message for a file whose name holds no culture reads as one sentence.

## `1.0.0-preview.13`

*November 15, 2026*

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
- Messages and documents speak of a key's entries and texts, and of its source text in the source language, in the
  words `Forma.Framework.Localization` uses, rather than of translations and English.

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
