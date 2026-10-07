namespace Forma.Locali.Sources;

/// <summary>A locale file with the anchors the runtime places it at in its culture.</summary>
/// <param name="Path">The file, as a full path.</param>
/// <param name="CultureName">The culture the file's own name gives it.</param>
/// <param name="AnchorNames">The full name of every type or namespace the runtime reads the file for, none when no path in force reads it.</param>
public sealed record PlacedLocaleFile(string Path, string CultureName, IReadOnlyList<string> AnchorNames);
