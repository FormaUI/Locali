namespace Forma.Locali.Projects;

using Microsoft.CodeAnalysis;

/// <summary>A project loaded far enough to analyse, and what the load could not do.</summary>
/// <param name="Path">The project file the compilation came from.</param>
/// <param name="Compilation">The compilation, or null when the project would not load.</param>
/// <param name="HasGeneratedSources">Whether generated C# for Razor markup was found on disk.</param>
/// <param name="HasRazorMarkup">Whether the project carries any Razor markup at all.</param>
/// <param name="RootNamespace">The root the project embeds its resources under, or null when it declares none.</param>
/// <param name="EmbeddedResourceNames">The name the build embeds each file under, keyed by the file's full path, or null when its items were not read, in which case every locale file beneath it is taken as embedded under the name the SDK would give it.</param>
/// <param name="IsLocalePack">Whether the project declares <c>IsLocalePack</c>, making it a locale pack: an assembly of nothing but locale files for other assemblies' types.</param>
public sealed record LoadedProject(
    string Path,
    Compilation? Compilation,
    bool HasGeneratedSources,
    bool HasRazorMarkup,
    string? RootNamespace = null,
    IReadOnlyDictionary<string, string>? EmbeddedResourceNames = null,
    bool IsLocalePack = false);
