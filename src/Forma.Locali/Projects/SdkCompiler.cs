namespace Forma.Locali.Projects;

using System.Runtime.CompilerServices;
using System.Runtime.Loader;

using Microsoft.Build.Locator;

/// <summary>Binds Roslyn to the compiler the installed SDK ships, which is the one its Razor generator is built against.</summary>
internal static class SdkCompiler
{
    private const string Compiler = "Microsoft.CodeAnalysis.CSharp.dll";

    /// <summary>Why the SDK's compiler could not be bound, or null when it was.</summary>
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
        var folder = sdk is null ? null : Path.Combine(sdk, "Roslyn", "bincore");

        if (folder is null || !File.Exists(Path.Combine(folder, Compiler)))
        {
            Unavailable =
                $"""
                 The linter runs on the C# compiler the installed SDK ships, so that the SDK's Razor
                 generator always loads into the compiler it was built against. None was found: no
                 '{Compiler}' in '{folder ?? "<no SDK located>"}'.
                 See 'Roslyn Is The SDK's Compiler' in the linter's README.
                 """;

            return;
        }

        // The linter ships no Roslyn of its own, so every Roslyn assembly resolves from the compiler's folder.
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var candidate = Path.Combine(folder, name.Name + ".dll");

            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        };
    }
}
