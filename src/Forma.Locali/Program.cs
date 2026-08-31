namespace Forma.Locali;

using Forma.Locali.Cli;
using Forma.Locali.Linting;
using Forma.Locali.Projects;
using Forma.Locali.Reporting;
using Forma.Locali.Rules;
using Forma.Locali.Rules.Files;
using Forma.Locali.Rules.Sources;
using Forma.Locali.Sources;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // Nothing can be analysed without Roslyn, so the run stops before a load failure buries the reason.
        if (SdkRoslyn.Unavailable is { } unavailable)
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
            var lint = host.Services.GetRequiredService<LintHostedService>();

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

        builder.Logging.AddSimpleConsole(console => console.SingleLine = true).SetMinimumLevel(LogLevel.Warning);
        builder.Services.Configure<ConsoleLifetimeOptions>(lifetime => lifetime.SuppressStatusMessages = true);

        builder.Services
            .AddOptions<LinterOptions>()
            .Configure(bound =>
            {
                bound.Path = options.Path;
                bound.ReportPath = options.ReportPath;
                bound.Excludes = options.Excludes;
                bound.MinimumSeverity = options.MinimumSeverity;
            })
            .Validate(
                bound => File.Exists(bound.Path) || Directory.Exists(bound.Path),
                $"'{options.Path}' is neither a file nor a directory.")
            .ValidateOnStart();

        builder.Services.AddSingleton<ILintTargetResolver, LintTargetResolver>();
        builder.Services.AddSingleton<ILocalizationFileLinter, LocalizationFileLinter>();
        builder.Services.AddSingleton<IProjectLoader, MSBuildProjectLoader>();
        builder.Services.AddSingleton<SemanticCallSiteFinder>();
        builder.Services.AddSingleton<SyntacticCallSiteFinder>();

        builder.Services.AddSingleton<ILocalizationFileRuleChecker, RequiredFieldsChecker>();
        builder.Services.AddSingleton<ILocalizationFileRuleChecker, RequiredFieldOrderChecker>();
        builder.Services.AddSingleton<ILocalizationFileRuleChecker, UnknownFieldChecker>();
        builder.Services.AddSingleton<ILocalizationFileRuleChecker, SchemaVersionChecker>();
        builder.Services.AddSingleton<ILocalizationFileRuleChecker, CultureMatchesChecker>();
        builder.Services.AddSingleton<ILocalizationFileRuleChecker, TranslationHolesChecker>();
        builder.Services.AddSingleton<ILocalizationFileRuleChecker, TranslationHoleShapeChecker>();
        builder.Services.AddSingleton<ILocalizationFileRuleChecker, DuplicateKeyChecker>();

        builder.Services.AddSingleton<ICallSiteRuleChecker, MalformedExpressionChecker>();
        builder.Services.AddSingleton<ICallSiteRuleChecker, ValueCountChecker>();
        builder.Services.AddSingleton<ICallSiteRuleChecker, PluralArgumentChecker>();
        builder.Services.AddSingleton<ICallSiteRuleChecker, SoleArgumentChecker>();
        builder.Services.AddSingleton<ICallSiteRuleChecker, PluralOtherFieldChecker>();
        builder.Services.AddSingleton<ICallSiteRuleChecker, HoleShapeChecker>();
        builder.Services.AddSingleton<ICallSiteRuleChecker, BareHoleKeyChecker>();
        builder.Services.AddSingleton<ICallSiteRuleChecker, BareExpressionKeyChecker>();

        builder.Services.AddSingleton<ISchemaLinter, SchemaLinter>();
        builder.Services.AddSingleton<ISourceLinter, SourceLinter>();
        builder.Services.AddSingleton<IReachabilityLinter, ReachabilityLinter>();
        builder.Services.AddSingleton<ITranslationCoverageLinter, TranslationCoverageLinter>();
        builder.Services.AddSingleton<ILintService, LintService>();

        builder.Services.AddSingleton<IDiagnosticReporter>(_ => new ConsoleDiagnosticReporter(Console.Out));

        // The report writer only exists when one was asked for, so nothing has to check for a missing path later.
        if (options.ReportPath is { } report)
        {
            builder.Services.AddSingleton<IDiagnosticReporter>(_ => new JsonDiagnosticReporter(report));
        }

        builder.Services.AddSingleton<LintHostedService>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<LintHostedService>());

        return builder.Build();
    }
}
