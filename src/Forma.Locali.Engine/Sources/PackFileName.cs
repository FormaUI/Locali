namespace Forma.Locali.Sources;

using Forma.Framework.Localization.Files;

/// <summary>What a locale pack's culture-named file is named, readable or not: the name the build embeds it under, what the runtime reads from that name, and every assembly the run builds or references that the name starts with.</summary>
/// <param name="ResourceName">The name the build embeds the file under, which the runtime reads it by.</param>
/// <param name="FileName">The culture and the name the runtime reads from the resource name.</param>
/// <param name="AssemblyNames">Every assembly the run builds or references that the name starts with, ordered by name; none for a name starting with no such assembly.</param>
public sealed record PackFileName(string ResourceName, LocaleFileName FileName, IReadOnlyList<string> AssemblyNames)
{
    /// <summary>The longest assembly name the name starts with, whose types the file is held to, or null where it starts with none.</summary>
    public string? AssemblyName { get; } = AssemblyNames.MaxBy(assemblyName => assemblyName.Length);
}
