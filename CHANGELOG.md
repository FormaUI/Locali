# Changelog

Each release of Locali, newest first. An entry is dated when its release is cut, and reads *Unreleased* until then.
The releases before `1.0.0-preview.11` are recorded by their [tags](https://github.com/FormaUI/Locali/tags) alone.

## `1.0.0-preview.12`

*Unreleased*

- A call to a method whose parameter carries `[LocaleKey]`, as `LocalizableText.For<TSource>`'s key does, is read
  as a lookup: its key is checked as a lookup's is, against the translations of the method's type argument, and a
  key passed on from a parameter carrying `[LocaleKey]` is checked where its own method is called.

## `1.0.0-preview.11`

*November 5, 2026*

- The package links each of its assemblies to the commit it was built from through Source Link, carries its symbols
  in a `.snupkg` beside it, and names this entry as its release notes.
- The package is published to nuget.org alone; the FormaUI organization's GitHub Packages feed receives no new
  version.
