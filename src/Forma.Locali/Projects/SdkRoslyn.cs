namespace Forma.Locali.Projects;

using System.Runtime.CompilerServices;
using System.Runtime.Loader;

using Microsoft.Build.Locator;

/// <summary>Binds Roslyn to the copy the installed SDK ships, which is the only one new enough to load its Razor generator.</summary>
internal static class SdkRoslyn
{
    private const string Workspaces = "Microsoft.CodeAnalysis.Workspaces.MSBuild.dll";

    private static readonly string[] Tools = ["dotnet-format", "dotnet-watch"];

    /// <summary>Why Roslyn could not be bound, or null when it was.</summary>
    public static string? Unavailable { get; private set; }

    // Resolution has to be in place before the first Roslyn type loads, which only a module initializer guarantees.
    [ModuleInitializer]
    internal static void Bind()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }

        var sdk = MSBuildLocator.QueryVisualStudioInstances().FirstOrDefault()?.MSBuildPath;

        if (Locate(sdk) is not { } folder)
        {
            Unavailable =
                $"""
                 The linter reads Razor markup by running the SDK's Razor generator, and Roslyn refuses to
                 load a generator built against a newer compiler than its host. The published
                 Microsoft.CodeAnalysis packages are older than that generator, so the linter binds to the
                 Roslyn the SDK itself ships. None was found: no '{Workspaces}' under
                 '{string.Join("' or '", Tools)}' in '{Path.Combine(sdk ?? "<no SDK located>", "DotnetTools")}'.
                 See 'Roslyn is loaded from the SDK' in the linter's README.
                 """;

            return;
        }

        // The folder supplies Roslyn's own dependencies too, so every name it holds resolves from it.
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var candidate = Path.Combine(folder, name.Name + ".dll");

            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        };
    }

    private static string? Locate(string? sdk)
    {
        if (sdk is null || Path.Combine(sdk, "DotnetTools") is not { } tools || !Directory.Exists(tools))
        {
            return null;
        }

        foreach (var tool in Tools)
        {
            var folder = Path.Combine(tools, tool);

            if (!Directory.Exists(folder))
            {
                continue;
            }

            if (Directory.EnumerateFiles(folder, Workspaces, SearchOption.AllDirectories).FirstOrDefault() is { } found)
            {
                return Path.GetDirectoryName(found);
            }
        }

        return null;
    }
}
