namespace Forma.Locali.Sources;

/// <summary>An assembly a project of the run builds, with the types it declares and the root namespace the runtime counts their names from.</summary>
/// <param name="AssemblyName">The assembly's name.</param>
/// <param name="TypeNames">The name of every type the assembly declares.</param>
/// <param name="RootNamespace">The root namespace the assembly's own config declares, or the assembly's name where it declares none.</param>
public sealed record BuiltAssembly(string AssemblyName, IReadOnlyList<string> TypeNames, string RootNamespace);
