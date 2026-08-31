namespace Forma.Locali.Projects;

using Microsoft.CodeAnalysis;

/// <summary>A project loaded far enough to analyse, and what the load could not do.</summary>
/// <param name="Path">The project file the compilation came from.</param>
/// <param name="Compilation">The compilation, or null when the project would not load.</param>
/// <param name="HasGeneratedSources">Whether generated C# for Razor markup was found on disk.</param>
/// <param name="HasRazorMarkup">Whether the project carries any Razor markup at all.</param>
/// <param name="RootNamespace">The root the project embeds its resources under, or null when it declares none.</param>
public sealed record LoadedProject(
    string Path,
    Compilation? Compilation,
    bool HasGeneratedSources,
    bool HasRazorMarkup,
    string? RootNamespace = null);
