namespace Forma.Locali;

using Forma.Locali.Cli;
using Forma.Locali.Configuration;
using Forma.Locali.Projects;
using Forma.Locali.Reporting;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // Nothing can be analysed without the compiler, so the run stops before a load failure buries the reason.
        if (SdkCompiler.Unavailable is { } unavailable)
        {
            await Console.Error.WriteLineAsync(unavailable).ConfigureAwait(false);

            return (int)ExitCode.Usage;
        }

        if (!CommandLine.TryParse(args, out var options, out var error))
        {
            await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
            await Console.Error.WriteLineAsync(CommandLine.Usage).ConfigureAwait(false);

            return (int)ExitCode.Usage;
        }

        using var host = Build(args, options!);

        try
        {
            // RunAsync disposes the provider before it returns, so the service has to be held from before the run.
            var lint = host.Services.GetRequiredService<LintingHostedService>();

            await host.RunAsync().ConfigureAwait(false);

            return (int)lint.ExitCode;
        }
        catch (OptionsValidationException exception)
        {
            await Console.Error.WriteLineAsync(string.Join(Environment.NewLine, exception.Failures)).ConfigureAwait(false);

            return (int)ExitCode.Usage;
        }
    }

    private static IHost Build(string[] args, LinterOptions options)
    {
        // An empty builder keeps the linter from reading whatever configuration happens to sit in the working directory.
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { Args = args });

        // Standard output carries the report alone, so every log line goes to standard error.
        builder.Logging
            .AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace)
            .AddSimpleConsole(console => console.SingleLine = true)
            .SetMinimumLevel(LogLevel.Warning);
        builder.Services.Configure<ConsoleLifetimeOptions>(lifetime => lifetime.SuppressStatusMessages = true);

        builder.Services
            .AddOptions<LinterOptions>()
            .Configure(bound =>
            {
                bound.Path = options.Path;
                bound.ReportPath = options.ReportPath;
                bound.Excludes = options.Excludes;
                bound.Properties = options.Properties;
                bound.MinimumSeverity = options.MinimumSeverity;
            })
            .Validate(
                bound => File.Exists(bound.Path) || Directory.Exists(bound.Path),
                $"'{options.Path}' is neither a file nor a directory.")
            .ValidateOnStart();

        // One instance for the whole run, so a glob's usage tracked while excluding files is the same
        // tracking read back while deciding what a file's own configuration leaves out.
        builder.Services.AddSingleton<ILintingConfiguration>(_ => new LintingConfiguration(options.Path, options.Excludes));
        builder.Services.AddLocaliEngine();

        builder.Services.AddSingleton<IDiagnosticReporter>(_ => new ConsoleDiagnosticReporter(Console.Out));

        // The report writer only exists when one was asked for, so nothing has to check for a missing path later.
        if (options.ReportPath is { } report)
        {
            builder.Services.AddSingleton<IDiagnosticReporter>(_ => new JsonDiagnosticReporter(report));
        }

        builder.Services.AddSingleton<LintingHostedService>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<LintingHostedService>());

        return builder.Build();
    }
}
