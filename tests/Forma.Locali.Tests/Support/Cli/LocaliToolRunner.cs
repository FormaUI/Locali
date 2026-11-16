namespace Forma.Locali.Tests.Support.Cli;

/// <summary>Runs the built linter as a process of its own, where MSBuild loads as it does for a user; the test host binds an older NuGet.Frameworks than MSBuild takes, so a project cannot be loaded inside it.</summary>
public static class LocaliToolRunner
{
    /// <summary>What a run of the linter wrote and exited with.</summary>
    /// <param name="cancellationToken">Ends the run, and the process with it.</param>
    /// <param name="arguments">The arguments the linter is started with.</param>
    public static Task<(int ExitCode, string Output)> RunAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return DotnetRunner.RunAsync(null, cancellationToken, [GetToolPath(), .. arguments]);
    }

    // The tests build the linter through their project reference, into its own output beside theirs: the same configuration and framework, under src.
    private static string GetToolPath()
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var framework = output.Name;
        var configuration = output.Parent!.Name;
        var repository = output.Parent.Parent!.Parent!.Parent!.Parent!.FullName;

        return Path.Combine(repository, "src", "Forma.Locali", "bin", configuration, framework, "locali.dll");
    }
}
