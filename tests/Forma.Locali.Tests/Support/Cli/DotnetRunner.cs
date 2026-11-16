namespace Forma.Locali.Tests.Support.Cli;

using System.Diagnostics;

/// <summary>Runs the <c>dotnet</c> host as a process of its own, for the linter itself or for what a scratch tree needs before the linter reads it.</summary>
public static class DotnetRunner
{
    /// <summary>What a run wrote and exited with.</summary>
    /// <param name="workingDirectory">The folder the run starts in, or null for the test's own.</param>
    /// <param name="cancellationToken">Ends the run, and the process with it.</param>
    /// <param name="arguments">The arguments the host is started with.</param>
    public static async Task<(int ExitCode, string Output)> RunAsync(string? workingDirectory, CancellationToken cancellationToken, params IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? string.Empty,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("The dotnet host did not start.");

        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);

            throw;
        }

        return (process.ExitCode, await output.ConfigureAwait(false) + await error.ConfigureAwait(false));
    }
}
