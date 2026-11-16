namespace Forma.Locali.Projects;

using Forma.Framework.Localization.Files;

using Microsoft.CodeAnalysis;

/// <summary>A project loaded far enough to analyse, and what the load could not do.</summary>
/// <param name="Path">The project file the compilation came from.</param>
/// <param name="Compilation">The compilation, or null when the project would not load or is written in a language the linter does not read.</param>
/// <param name="HasGeneratedSources">Whether generated C# for Razor markup was found on disk.</param>
/// <param name="HasRazorMarkup">Whether the project carries any Razor markup at all.</param>
/// <param name="RootNamespace">The root the project embeds its resources under, or null when it declares none.</param>
/// <param name="EmbeddedResourceNames">The name the build embeds each file under, keyed by the file's full path, or null when its items were not read, in which case every locale file beneath it is taken as embedded under the name the build would give it.</param>
/// <param name="IsLocalePack">Whether the project declares <c>IsLocalePack</c>, making it a locale pack: an assembly of nothing but locale files for other assemblies' types.</param>
/// <param name="SatelliteCultures">The culture of the satellite assembly the build moves each such embedded file into, keyed by the file's full path, or null when its items were not read.</param>
public sealed record LoadedProject(
    string Path,
    Compilation? Compilation,
    bool HasGeneratedSources,
    bool HasRazorMarkup,
    string? RootNamespace = null,
    IReadOnlyDictionary<string, string>? EmbeddedResourceNames = null,
    bool IsLocalePack = false,
    IReadOnlyDictionary<string, string>? SatelliteCultures = null)
{
    /// <summary>The assembly the build runs in place of each one the compiler reads where the two differ, keyed by the full path the compiler reads: a referenced project's own assembly for its reference assembly, or a package's runtime assembly for its compile-time one.</summary>
    public IReadOnlyDictionary<string, string> RuntimeAssemblyPaths { get; init; } = new Dictionary<string, string>();

    /// <summary>Whether the project declares <c>IsTestProject</c>, whose calls and registrations never speak for product code.</summary>
    public bool IsTestProject { get; init; }

    /// <summary>Whether the project's packages were restored, or it references none; a project loaded without its restore output reads none of the types its packages declare.</summary>
    public bool IsRestored { get; init; } = true;

    /// <summary>The folder each package the project compiles against was restored into, by the package's identifier.</summary>
    public IReadOnlyDictionary<string, string> PackageRoots { get; init; } = new Dictionary<string, string>();

    /// <summary>The culture the name a satellite assembly holds a file under gives it where that is not the satellite's culture, so the runtime reads the file under no name, or null where the runtime reads it.</summary>
    /// <param name="fullPath">The file, as a full path.</param>
    public string? GetCultureOutsideSatellite(string fullPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(fullPath);

        if (SatelliteCultures?.GetValueOrDefault(fullPath) is not { } satelliteCulture || EmbeddedResourceNames?.GetValueOrDefault(fullPath) is not { } name)
        {
            return null;
        }

        // A satellite's file is read for the first culture its name holds, folders and dots alike separating its parts.
        var nameCulture = name[..^System.IO.Path.GetExtension(name).Length].Split('.', '/').FirstOrDefault(part => LocaleFileNames.IsCulture(part));

        return nameCulture is not null && nameCulture != satelliteCulture ? nameCulture : null;
    }
}
