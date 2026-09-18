namespace Forma.Locali.Sources;

using System.Globalization;

using Forma.Framework.Localization.Files;
using Forma.Locali.Rules;

/// <summary>A project's translation file, read once, with the name the runtime reads it by.</summary>
/// <param name="Path">The file, as a full path.</param>
/// <param name="Culture">The culture the file's own name gives it.</param>
/// <param name="Name">The name the runtime reads the file by, as the build embeds it.</param>
/// <param name="File">The file's sections and entries.</param>
/// <param name="Ignored">The rules the file's tools entry leaves it out of.</param>
public sealed record TranslationFile(string Path, CultureInfo Culture, string Name, LocaleFile File, IgnoredRules Ignored);
