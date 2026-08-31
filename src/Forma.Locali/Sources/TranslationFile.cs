namespace Forma.Locali.Sources;

using System.Globalization;

using Microsoft.CodeAnalysis;

/// <summary>A project's translation file, paired with the type the runtime resolves it by.</summary>
/// <param name="Path">The file, as a full path.</param>
/// <param name="Culture">The culture the file's own name gives it.</param>
/// <param name="Type">The type that answers to the file's location, or null when none does.</param>
/// <param name="ExpectedName">The full name the default convention gives the file.</param>
public sealed record TranslationFile(string Path, CultureInfo Culture, INamedTypeSymbol? Type, string ExpectedName);
