namespace Forma.Locali;

using Forma.Locali.Cli;
using Forma.Locali.Documents;
using Forma.Locali.Linting;
using Forma.Locali.Projects;
using Forma.Locali.Rules;
using Forma.Locali.Rules.Files;
using Forma.Locali.Rules.Projects;
using Forma.Locali.Rules.Sources;
using Forma.Locali.Sources;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the linting engine in a service collection.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the locale format, target resolution, project loading, every rule, and the linters that run them; the host supplies the <see cref="LinterOptions"/> and the <see cref="Configuration.ILintingConfiguration"/> a run applies.</summary>
    public static IServiceCollection AddLocaliEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ILocaleFormat, JsonLocaleFormat>();
        services.AddSingleton<ILintingTargetResolver, LintingTargetResolver>();
        services.AddSingleton<ILocalizationFileLinter, LocalizationFileLinter>();
        services.AddSingleton<IProjectLoader, MSBuildProjectLoader>();
        // The finders are asked in this order, so the semantic one answers wherever the contract binds.
        services.AddSingleton<ILocalizerCallsiteFinder, SemanticCallsiteFinder>();
        services.AddSingleton<ILocalizerCallsiteFinder, CSharpSyntacticCallsiteFinder>();
        services.AddSingleton<ILocalizerCallsiteFinder, VisualBasicSyntacticCallsiteFinder>();

        services.AddSingleton<ILocalizationFileRuleChecker, RequiredFieldsChecker>();
        services.AddSingleton<ILocalizationFileRuleChecker, RequiredFieldOrderChecker>();
        services.AddSingleton<ILocalizationFileRuleChecker, UnknownFieldChecker>();
        services.AddSingleton<ILocalizationFileRuleChecker, SchemaVersionChecker>();
        services.AddSingleton<ILocalizationFileRuleChecker, CultureMatchesChecker>();
        services.AddSingleton<ILocalizationFileRuleChecker, LocalizedTextHolesChecker>();
        services.AddSingleton<ILocalizationFileRuleChecker, LocalizedTextHoleShapeChecker>();
        services.AddSingleton<ILocalizationFileRuleChecker, DuplicateKeyChecker>();
        services.AddSingleton<ILocalizationFileRuleChecker, InconsistentLocalizationChecker>();
        services.AddSingleton<ILocalizationFileRuleChecker, ReservedEntryOrderChecker>();
        services.AddSingleton<ILocalizationFileRuleChecker, ReservedKeyShapeChecker>();
        services.AddSingleton<ILocalizationFileRuleChecker, SharedEntryChecker>();

        services.AddSingleton<ICallsiteRuleChecker, MalformedExpressionChecker>();
        services.AddSingleton<ICallsiteRuleChecker, ValueCountChecker>();
        services.AddSingleton<ICallsiteRuleChecker, PluralArgumentChecker>();
        services.AddSingleton<ICallsiteRuleChecker, SoleArgumentChecker>();
        services.AddSingleton<ICallsiteRuleChecker, PluralOtherFieldChecker>();
        services.AddSingleton<ICallsiteRuleChecker, HoleShapeChecker>();
        services.AddSingleton<ICallsiteRuleChecker, BareHoleKeyChecker>();
        services.AddSingleton<ICallsiteRuleChecker, BareExpressionKeyChecker>();
        services.AddSingleton<ICallsiteRuleChecker, ReservedLookupShapeChecker>();
        services.AddSingleton<ICallsiteRuleChecker, ReformattedLookupChecker>();

        services.AddSingleton<IProjectRuleChecker, LocaleConfigPlacementChecker>();
        services.AddSingleton<IProjectRuleChecker, RootNamespaceWithoutTypesChecker>();
        services.AddSingleton<IProjectRuleChecker, UnreachableLocalizationChecker>();
        services.AddSingleton<IProjectRuleChecker, LocalizationCoverageChecker>();
        services.AddSingleton<IProjectRuleChecker, DuplicateAnchorFileChecker>();
        services.AddSingleton<IProjectRuleChecker, TypeNamedFolderChecker>();
        services.AddSingleton<IProjectRuleChecker, CultureCoverageChecker>();
        services.AddSingleton<IProjectRuleChecker, LocalePackFolderChecker>();
        services.AddSingleton<IProjectRuleChecker, LocaleSourceChecker>();

        services.AddSingleton<ISchemaLinter, SchemaLinter>();
        services.AddSingleton<ISourceLinter, SourceLinter>();
        services.AddSingleton<IProjectLinter, ProjectLinter>();
        services.AddSingleton<ILintingService, LintingService>();

        return services;
    }
}
