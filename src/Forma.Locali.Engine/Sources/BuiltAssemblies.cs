namespace Forma.Locali.Sources;

using Forma.Framework.Localization.Files;
using Forma.Locali.Projects;

/// <summary>Resolves the assemblies the projects of a run build, which an application's files for other assemblies are read against.</summary>
public static class BuiltAssemblies
{
    /// <summary>Every assembly a compiled project of the run builds, a locale pack's aside, each with its types and the root namespace its config declares; an assembly whose config the runtime would refuse, or whose items were not read, is left out, since its root namespace cannot be told.</summary>
    /// <param name="projects">Every project the run loaded.</param>
    public static IReadOnlyList<BuiltAssembly> ResolveAll(LoadedProjects projects)
    {
        ArgumentNullException.ThrowIfNull(projects);

        var assemblies = new Dictionary<string, BuiltAssembly>(StringComparer.Ordinal);

        foreach (var project in projects.All)
        {
            if (project.IsLocalePack
                || project.EmbeddedResourceNames is null
                || project.Compilation is not { AssemblyName: { Length: > 0 } assemblyName } compilation
                || ReadRootNamespace(project, assemblyName) is not { } rootNamespace)
            {
                continue;
            }

            // Projects building one assembly, one per platform, declare its types between them.
            IReadOnlyList<string> typeNames = ResourceTypeNames.GetAll(compilation);
            if (assemblies.TryGetValue(assemblyName, out var built))
            {
                typeNames = [.. built.TypeNames.Union(typeNames, StringComparer.Ordinal)];
                rootNamespace = built.RootNamespace;
            }

            assemblies[assemblyName] = new BuiltAssembly(assemblyName, typeNames, rootNamespace);
        }

        return [.. assemblies.Values];
    }

    // The root namespace the runtime counts the assembly's names from, or null where it would refuse the assembly's config.
    private static string? ReadRootNamespace(LoadedProject project, string assemblyName)
    {
        var configPaths = ProjectLocaleFiles.GetConfigPaths(project);
        if (configPaths.Count == 0)
        {
            return assemblyName;
        }

        if (configPaths.Count > 1)
        {
            return null;
        }

        var configPath = configPaths.Values.Single();

        try
        {
            using var stream = File.OpenRead(configPath);

            return LocaleConfig.Read(stream, configPath).RootNamespace ?? assemblyName;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or LocaleConfigException)
        {
            return null;
        }
    }
}
