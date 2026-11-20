# Changelog

Each release of Locali, newest first. An entry is dated when its release is cut, and reads *Unreleased* until then.
The releases before `1.0.0-preview.11` are recorded by their [tags](https://github.com/FormaUI/Locali/tags) alone.

## `1.0.0-preview.26`

*Unreleased*

- Breaking: a lookup's values pair with the arguments its key's expressions name as well as its holes, in the order
  each name first appears, as Forma's runtime pairs them since its `gender` expression. LOC0201 counts both, so
  `L["Found [$ [count] plural; …; $]", 1]` is right and a gendered key taking its gender and a name takes two values,
  and LOC0200 no longer reports an argument a value pairs with.
- `gender` is a keyword the linter reads: a field it does not define is LOC0203, an expression naming no argument is
  LOC0202's as a plural one is, and LOC0210 reads its fields. LOC0200 reports plural expressions alone.

## `1.0.0-preview.25`

*November 17, 2026*

- LOC0217 holds a type to entries of its own, as LOC0119 does. A type that a `$shared` block above it reaches in one
  culture and nothing reaches in another is reported in the second, where the block's entries kept every rule silent
  before. A library's type that an application's files localize in some of the library's cultures is reported in the
  others, which no rule checked.

## `1.0.0-preview.24`

*November 17, 2026*

- A lookup in a test project is followed through the test's own registrations beside product code's. One reaching a
  locale factory the test registers itself, as a stub answering its keys does, is held to no type and counted unchecked,
  rather than to the type the host's factory would read for it, and a service is followed to the one implementation
  product code and the test register between them, so one the test registers otherwise than product code does has none.
  A lookup in product code never reads a test's registrations.
- LOC0217 warns of a type lookups read that has no entries in any culture its assembly's locale files carry, once per
  type and culture at the first lookup reading it. Such a type was taken for one left unlocalized on purpose, so a new
  component whose section was never written rendered every key in the source language with no rule reporting it.

## `1.0.0-preview.23`

*November 17, 2026*

- Breaking: LOC0208, LOC0214 and LOC0215 are retired, LOC0208 at a library's public method included. A lookup the run
  could not check is no diagnostic: one writing its key as an expression, one taking its key from a parameter some call
  writes otherwise, and one from which no path reaches a type are counted unchecked, the console summary says how many,
  and the JSON report's `coverage.uncheckedLookups` lists each with its place, its reasons, `keyNotWritten` and
  `keyFromParameter` among them, and what kept it unchecked, naming for a library's public method the key hint that lets
  its consumers' runs check the calls they make. `coverage.lookups` counts those two kinds of key as well.
- A linting configuration naming a retired rule in `ignoreDiagnostics` is reported as LOC0021, a warning, rather than
  LOC0020, so a configuration written for an earlier release keeps its run passing until the line is dropped.

## `1.0.0-preview.22`

*November 17, 2026*

- A key a lookup takes from a parameter is settled call by call through the method's paths, carrying what each call
  passes: a condition the call's constants decide, such as an empty or null test of the key or a flag it passes, is
  followed the way it goes, and one on anything but the key leaves the lookup reachable. A call the method returns from
  before the lookup, such as `Text("")` past `if (key.Length == 0) return key;`, is no longer checked as a key, and a
  key looked up past such a condition is checked at its calls in place of LOC0208, which stays where a condition on the
  key the flow cannot settle stands between a call and the lookup, or the method writes the parameter first.
- Breaking: `hints.packages` is gone. A library's `.locali-hints.json` travels with its package: every run restoring the
  package reads it unasked, so an application deriving from a library's component is checked through the library's hints
  with nothing to write, and what is wrong in a package's file is reported as a warning, since the run cannot change it.
  A library's file, at its project or its package, names only the assemblies the library builds or ships, and LOC0028
  reports a hint for another.
- A `localeKeys` hint names a method that looks a parameter up as a key, every overload alike, and where the locale it
  reads comes from: a `locale` parameter or member, or a `typeParameter`. Where a project of the run builds the method,
  the hint is held to its body, LOC0029 reporting one the body does not bear out, and a hint it bears out answers for a
  library's consumers, so LOC0208 no longer reports a public helper's key as one they may write. Where the run only
  references the method, as an application restoring the library's package, every call to it is a lookup of its key,
  checked in that locale, followed through a method passing its own parameter on, and reported as LOC0208 where it is an
  expression.

## `1.0.0-preview.21`

*November 17, 2026*

- Breaking: `[LocaleKey]` is no longer read. A call to `LocalizableText.For<TSource>` is a lookup of its key in
  `TSource`'s entries because it is the runtime's own, read alike whether the runtime is a package or a project of the
  run, and a method of another type named `For` is not.
- A key a lookup takes from a parameter of its own method is checked at each call the run makes to the method that
  writes it as text, held to the type the lookup reads when that call reaches it and followed through calls passing it
  on, so a helper such as `string Label(string key) => L[key]` or a wrapper of `LocalizableText.For` needs no marking.
  LOC0208 reports it where a call writes it otherwise, where calls the run cannot see may write it, or where a call's
  text may not be what the lookup reads, as under a condition.
- LOC0216 reports, as an error, a project referencing packages that was loaded without its restore output, whose
  packages' types went unread, in place of a run that reads clean while checking less than it would.

## `1.0.0-preview.20`

*November 17, 2026*

- LOC0207 reads a type's entries and cultures from the assembly declaring it, as `Forma.Framework.Localization` reads
  them, rather than from the project holding the lookup, so a lookup reading a type of another project of the run is
  held to that project's files, a type sharing its full name with one of another assembly is held to its own, and a
  type no project of the run builds is left alone. An application's own files for that assembly still answer.
- LOC0207 finds a lookup's type by following its locale back to where `Forma.Framework.Localization` decided it, through
  locals, fields, properties, parameters at each call the run makes or as the container passes them to a constructor no
  code calls, constructors, lambdas, local functions, delegates, lazy values and calls to the implementation the run
  registers, so a helper or a model given a locale by its callers, and a component whose base reaches the registered
  factory through a context, are held to their types' files. A value typed `IStringLocale<T>` is followed further where
  a type of the run derives from `T`, since the contract is covariant. A locale from a factory the code builds itself, a
  service registered in a way the flow cannot read, and alternatives the flow cannot pair with the lookup are left
  unchecked.
- Hints state what the linter cannot read in the code: `localeMembers` relates a member's locale to the type a member of
  the same instance returns. An application writes them under `hints` in `.locali.json`; a library keeps them in a
  `.locali-hints.json` at its project's root, read by every run building it, and ships it at its package's root, read
  where an application's `hints.packages` names the package. The flow is followed without hints first, so a hint is
  verified where it reaches the same type, set aside where it reaches another, and used where it reaches none, as over a
  compiled library; LOC0207 names the hint a finding rests on.
- LOC0028 reports a hint naming a type, a member or a package that is not there or not of the kind the hint reads, and
  LOC0029 two hints relating one member to different type members, or a hint the flow contradicts.
- Breaking: `[LocaleSource]` is no longer read; a hint states the relationship it carried, and LOC0214 reports a hinted
  member whose type member tells no type.
- LOC0207 holds a lookup through Microsoft's `IStringLocalizer<T>` with a literal key to `T`'s entries where the run has
  Forma's adapter answer it, registering `AddFormaStringLocalizerAdapter` ahead of any of Microsoft's localization; no
  other rule reads such a lookup, since the localizer fills a composite key with `string.Format`.
- LOC0215 reports, as a warning, a lookup writing its key in a localized project from which no path reaches a type its
  locale is read for, naming where a path ended.
- The JSON report carries a `coverage` object: how many lookups writing their keys were fully, partly or not checked,
  what established the types they were held to, and why the rest went unchecked.

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
