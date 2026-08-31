---
name: forma-localization
description: The Forma localization format this linter reads — IStringLocale lookups keyed by English source text, the {Type}.{culture}.json file resolved from the type's full name, the $localization metadata entry, brace holes and plural expressions, null entries, and parent-culture fallback. Use when writing or changing a rule, a fixture, or anything that parses the format, and to keep straight what the runtime defines versus what the linter merely reports.
---

# The localization format

The runtime lives in the sibling Forma repository as `Forma.Framework.Localization` and its `.Json` implementation; this linter compiles against it and must never re-implement its parsing. What follows is the shape of the data the rules read. The rule-by-rule authority is `src/Forma.Locali/README.md`.

## Lookups

- A consumer injects `IStringLocale<T>` and reads strings as `L["The English sentence"]`; the key **is** the English source text, and an absent translation falls back to the key, silently.
- A lookup takes one form: a literal key with values paired to its distinct holes by position, in the order the holes first appear — `L["Stored {stored} of {total}.", written, allowed]`. A wrong count throws (LOC0202).
- An interpolated key — `L[$"…{stored}…"]` — formats before the lookup, so the runtime receives the formatted text as the key and no translation matches it; LOC0213 reports every such call site as an error.
- Consumers name holes in PascalCase as a matter of style; the linter does not enforce it, and no rule should start to — a hole name is legal wherever the runtime reads it.

## Files

- A type's translations live in `{Type}.{culture}.json` files resolved from the type's **full name**, never from where the file sits, which is the drift LOC0112 exists to catch.
- The file's first property is the metadata entry: `"$localization": "SchemaVersion: 1.0.0; Culture: he;"` — required fields in that order, and the culture must agree with the file's own name.
- Every other property maps English source text to its translation. A JSON `null` value declares that the key's right translation *is* the source text; the runtime reads it as found, which differs from absent wherever a caller can tell (`ResourceNotFound`, `RequireMatchingLocalization`, `GetAllStrings`).
- A parent culture answers for its children: `Widget.es-419.json` may carry only what differs from `Widget.es.json`, and only a key neither declares is missing.

## Holes and expressions

- A brace hole `{Name}` names a value; a repeated name still takes one value, because that is how the runtime pairs them.
- A plural expression names its argument in brackets: `[$ plural; one: # file; other: # files; $][count]` — `#` renders the count, and `other` is the category the rest fall back to.
- A brace hole and a bracketed plural argument are equivalent ways of naming a value, so a key and its translation may mix them freely.
- Escaped braces are literal text, and brace text containing a space is prose the runtime renders as written.
- Do use the runtime's own `LocalizedFormat` parsing (through `HoleNames`) to read the names a format refers to; scanning for braces by hand re-implements the runtime and will disagree with it.
