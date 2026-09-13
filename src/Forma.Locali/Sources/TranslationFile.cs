namespace Forma.Locali.Sources;

using System.Globalization;

using Forma.Framework.Localization.Files;

/// <summary>A project's translation file, read once, with every name the build could embed it under.</summary>
/// <param name="Path">The file, as a full path.</param>
/// <param name="Culture">The culture the file's own name gives it.</param>
/// <param name="ExpectedName">The full name the default embedding convention gives the file, without its culture.</param>
/// <param name="Names">Every name the runtime could ask for the file under, the conventional one first, each with the culture and extension.</param>
/// <param name="File">The file's sections and entries.</param>
public sealed record TranslationFile(string Path, CultureInfo Culture, string ExpectedName, IReadOnlyList<string> Names, LocaleFile File);
