namespace Forma.Locali.Sources;

using System.Globalization;

/// <summary>The keys one translation file answers for.</summary>
/// <param name="Path">The file the keys were read from.</param>
/// <param name="Culture">The culture the file's own name gives it.</param>
/// <param name="Keys">Every key the file declares, whatever it translates them to.</param>
internal sealed record TranslationKeys(string Path, CultureInfo Culture, HashSet<string> Keys);
