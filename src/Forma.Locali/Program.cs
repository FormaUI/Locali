namespace Forma.Locali;

using Forma.Locali.Cli;
using Forma.Locali.Configuration;
using Forma.Locali.Documents;
using Forma.Locali.Linting;
using Forma.Locali.Projects;
using Forma.Locali.Reporting;
using Forma.Locali.Rules;
using Forma.Locali.Rules.Files;
using Forma.Locali.Rules.Projects;
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
        builder.Services.AddSingleton<ILocaleFormat, JsonLocaleFormat>();
        builder.Services.AddSingleton<ILintingTargetResolver, LintingTargetResolver>();
        builder.Services.AddSingleton<ILocalizationFileLinter, LocalizationFileLinter>();
        builder.Services.AddSingleton<IProjectLoader, MSBuildProjectLoader>();
        // The finders are asked in this order, so the semantic one answers wherever the contract binds.
        builder.Services.AddSingleton<ILocalizerCallsiteFinder, SemanticCallsiteFinder>();
        builder.Services.AddSingleton<ILocalizerCallsiteFinder, CSharpSyntacticCallsiteFinder>();
        builder.Services.AddSingleton<ILocalizerCallsiteFinder, VisualBasicSyntacticCallsiteFinder>();

        builder.Services.AddSingleton<ILocalizationFileRuleChecker, RequiredFieldsChecker>();
        builder.Services.AddSingleton<ILocalizationFileRuleChecker, RequiredFieldOrderChecker>();
        builder.Services.AddSingleton<ILocalizationFileRuleChecker, UnknownFieldChecker>();
        builder.Services.AddSingleton<ILocalizationFileRuleChecker, SchemaVersionChecker>();
        builder.Services.AddSingleton<ILocalizationFileRuleChecker, CultureMatchesChecker>();
        builder.Services.AddSingleton<ILocalizationFileRuleChecker, LocalizedTextHolesChecker>();
        builder.Services.AddSingleton<ILocalizationFileRuleChecker, LocalizedTextHoleShapeChecker>();
        builder.Services.AddSingleton<ILocalizationFileRuleChecker, DuplicateKeyChecker>();
        builder.Services.AddSingleton<ILocalizationFileRuleChecker, InconsistentLocalizationChecker>();
        builder.Services.AddSingleton<ILocalizationFileRuleChecker, ReservedEntryOrderChecker>();
        builder.Services.AddSingleton<ILocalizationFileRuleChecker, ReservedKeyShapeChecker>();
        builder.Services.AddSingleton<ILocalizationFileRuleChecker, SharedEntryChecker>();

        builder.Services.AddSingleton<ICallsiteRuleChecker, MalformedExpressionChecker>();
        builder.Services.AddSingleton<ICallsiteRuleChecker, ValueCountChecker>();
        builder.Services.AddSingleton<ICallsiteRuleChecker, PluralArgumentChecker>();
        builder.Services.AddSingleton<ICallsiteRuleChecker, SoleArgumentChecker>();
        builder.Services.AddSingleton<ICallsiteRuleChecker, PluralOtherFieldChecker>();
        builder.Services.AddSingleton<ICallsiteRuleChecker, HoleShapeChecker>();
        builder.Services.AddSingleton<ICallsiteRuleChecker, BareHoleKeyChecker>();
        builder.Services.AddSingleton<ICallsiteRuleChecker, BareExpressionKeyChecker>();
        builder.Services.AddSingleton<ICallsiteRuleChecker, ReservedLookupShapeChecker>();
        builder.Services.AddSingleton<ICallsiteRuleChecker, ReformattedLookupChecker>();

        builder.Services.AddSingleton<IProjectRuleChecker, LocaleConfigPlacementChecker>();
        builder.Services.AddSingleton<IProjectRuleChecker, RootNamespaceWithoutTypesChecker>();
        builder.Services.AddSingleton<IProjectRuleChecker, UnreachableLocalizationChecker>();
        builder.Services.AddSingleton<IProjectRuleChecker, LocalizationCoverageChecker>();
        builder.Services.AddSingleton<IProjectRuleChecker, DuplicateAnchorFileChecker>();
        builder.Services.AddSingleton<IProjectRuleChecker, TypeNamedFolderChecker>();
        builder.Services.AddSingleton<IProjectRuleChecker, CultureCoverageChecker>();
        builder.Services.AddSingleton<IProjectRuleChecker, LocalePackFolderChecker>();
        builder.Services.AddSingleton<IProjectRuleChecker, LocaleSourceChecker>();

        builder.Services.AddSingleton<ISchemaLinter, SchemaLinter>();
        builder.Services.AddSingleton<ISourceLinter, SourceLinter>();
        builder.Services.AddSingleton<IProjectLinter, ProjectLinter>();
        builder.Services.AddSingleton<ILintingService, LintingService>();

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
