namespace Forma.Locali.Sources;

using Forma.Locali.Projects;

/// <summary>An assembly a locale pack's files name, with the files whose names start with its name and no longer one's, resolved against the projects of the run.</summary>
/// <param name="AssemblyName">The assembly's name, which the files' names start with.</param>
/// <param name="Files">The pack's readable culture-named files whose names start with the assembly's name and no longer assembly name the run knows, each with the name the runtime reads it by.</param>
/// <param name="Target">The project in the run that builds the assembly, or null when none does.</param>
public sealed record LocalePackAssembly(string AssemblyName, IReadOnlyList<ProjectLocaleFile> Files, LoadedProject? Target);
