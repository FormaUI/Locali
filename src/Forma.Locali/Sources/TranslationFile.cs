namespace Forma.Locali.Sources;

using System.Globalization;

using Forma.Framework.Localization.Files;
using Forma.Locali.Rules;

/// <summary>A project's translation file, read once, with the name the build embeds it under.</summary>
/// <param name="Path">The file, as a full path.</param>
/// <param name="Culture">The culture the file's own name gives it.</param>
/// <param name="ExpectedName">The full name the embedding convention gives the file, without its culture.</param>
/// <param name="Name">The name the build embeds the file under, with the culture and extension.</param>
/// <param name="File">The file's sections and entries.</param>
/// <param name="Ignored">The rules the file's tools entry leaves it out of.</param>
public sealed record TranslationFile(string Path, CultureInfo Culture, string ExpectedName, string Name, LocaleFile File, IgnoredRules Ignored);
