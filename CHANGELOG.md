# Changelog

Each release of Locali, newest first. An entry is dated when its release is cut, and reads *Unreleased* until then.
The releases before `1.0.0-preview.11` are recorded by their [tags](https://github.com/FormaUI/Locali/tags) alone.

## `1.0.0-preview.20`

*Unreleased*

- LOC0207 reads a type's entries and cultures from the assembly declaring it, as `Forma.Framework.Localization` reads
  them, rather than from the project holding the lookup, so a lookup reading a type of another project of the run is
  held to that project's files, a type sharing its full name with one of another assembly is held to its own, and a
  type no project of the run builds is left alone. An application's own files for that assembly still answer.
- LOC0207 finds a lookup's type by following its locale back to where `Forma.Framework.Localization` decided it, through
  locals, fields, properties, parameters at each call the run makes, constructors, lambdas, local functions, delegates,
  lazy values and calls to the implementation the run registers, so a helper or a model given a locale by its callers,
  and a component whose base reaches the registered factory through a context, are held to their types' files. A value
  typed `IStringLocale<T>` is followed further where a type of the run derives from `T`, since the contract is
  covariant. A locale from a factory the code builds itself, a service registered in a way the flow cannot read, and
  alternatives the flow cannot pair with the lookup are left unchecked.

## `1.0.0-preview.19`

*November 16, 2026*

- LOC0207 reads the property `[LocaleSource]` names in the compiled code of the assembly the build runs where no
  project of the run builds it, as where an application takes `FormaComponentBase` from a package, so a component
  deriving from it is held to its own files where it was left unchecked. A getter that returns `typeof` of a type or
  the instance's `GetType()` in one straight run is read, from the referenced assembly or, where the compiler reads
  a reference assembly, from the one the build puts in its place.
- LOC0214 reports, as a warning, a lookup through a member carrying `[LocaleSource]` whose named property's getter
  returns no type that could be read, in source or in compiled code, so its key went unchecked.

## `1.0.0-preview.18`

*November 16, 2026*

- LOC0119 and LOC0207 read an application's types from its files named for its own assembly too, as
  `Forma.Framework.Localization` reads them, rather than from its other files and its packs alone, so a key or a
  culture such a file answers is no longer reported as missing.
- LOC0207 holds a lookup through the unbound `IStringLocale` made through a member carrying `[LocaleSource]`, as
  `FormaComponentBase.L` is, to the type the property it names returns for each type of instance running the
  lookup: `typeof(X)` names `X`, and `GetType()` the instance's own type. A property another project of the run
  builds is read in that project's source, so a component deriving from `FormaComponentBase` is held to its own
  files where it was left unchecked.

## `1.0.0-preview.17`

*November 16, 2026*

- LOC0123 reports, as a warning, a part of the name a locale file is embedded under that is a culture other than the
  file's own and names no type, namespace or assembly the run knows, as `he` is in `.locales/es/he.json`, which the
  runtime reads as Spanish entries for a namespace `he`.
- A part of a locale file's name repeating its culture is read as that culture again, as
  `Forma.Framework.Localization` reads it, rather than as part of the name, so `.locales/es/es.json` is the root's
  file and `.locales/es/Components.Widget.es.json` stands for `Components.Widget`.
- A project's files are read by the names the build now embeds them under, as `Forma.Framework.Localization` reads
  them: the root namespace, a `/` and the file's path, such as `Contoso.App/Uploads/.locales/he.json`, and
  `Contoso.App/.locale.json` for its `.locale.json`, which went unread under these names, so LOC0023 reported the
  root namespace it declares and LOC0112 the files laid out by it. An application's files for other assemblies are
  read beneath its root and a `/` alone, a satellite's file whose name holds its culture keeps that name, and of two
  files giving one entry different texts, the one LOC0120 sets aside is the one whose path sorts first.
- An application with no `.locale.json` has its files for other assemblies read beneath the root namespace its build
  records on `LocaleApplicationAttribute`, as `Forma.Framework.Localization` reads them, rather than beneath its name,
  so where the two differ, LOC0112, LOC0120 and LOC0121 hold such a file to the other assembly's types.
- A file a satellite assembly holds under a name whose culture is not the satellite's is left unread, as
  `Forma.Framework.Localization` lists it in no culture, where it was read for the culture its name gives it, and
  LOC0124 reports it, as an error, since none of its entries is ever read.

## `1.0.0-preview.16`

*November 15, 2026*

- LOC0025, LOC0101 and LOC0108 read a file in a project without the folder of locale files its `.locale.json`
  names as `localesDir`, as the build embeds it and `Forma.Framework.Localization` reads it, rather than without
  `.locales` alone, so a file in that folder is held to the culture its name gives it and to the sources
  `localeSources` allows. A file in the host's `.locales` folder at the project's root is read without `.locales`,
  as the host reads it.
- Where a locale file's name carries no culture, LOC0122 names a part that names no type, namespace or assembly the
  run knows before one that does, rather than the first part that is a culture in another case.

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
