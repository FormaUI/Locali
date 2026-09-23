namespace Forma.Locali.Rules.Projects;

using Forma.Framework.Localization.Files;
using Forma.Locali.Configuration;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;
using Forma.Locali.Projects;

/// <summary>Checks that every locale file comes from a source the linting configuration in force over it allows.</summary>
/// <param name="lintingConfiguration">The linting configuration, read for the sources each file's ancestry allows.</param>
public sealed class LocaleSourceChecker(ILintingConfiguration lintingConfiguration) : IProjectRuleChecker
{
    private readonly ILintingConfiguration _lintingConfiguration = lintingConfiguration ?? throw new ArgumentNullException(nameof(lintingConfiguration));

    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.DisallowedLocaleSource;

    /// <inheritdoc />
    public void Check(ProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var project = context.Project;
        var hostFolder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(project.Path))!, LocaleConfig.DefaultLocalesDir) + Path.DirectorySeparatorChar;

        foreach (var file in context.LocalizationFiles)
        {
            var fullPath = Path.GetFullPath(file);
            if (LocalizationFileName.GetCulture(fullPath) is null)
            {
                continue;
            }

            // The configuration is asked about every locale file before anything else, so an entry over a project whose items were not read still counts as one that governs a file.
            if (_lintingConfiguration.GetLocaleSources(fullPath) is not { } allowed)
            {
                continue;
            }

            // Without the project's items no file is known to be embedded, so where a file comes from cannot be told.
            if ((!project.IsLocalePack && project.EmbeddedResourceNames is null)
                || GetSource(project, fullPath, hostFolder) is not { } source
                || (allowed & source) != 0)
            {
                continue;
            }

            context.Report(
                Code,
                fullPath,
                $"This file is {Describe(source)}, which the linting configuration in force here does not allow, naming {LocaleSourceNames.Format(allowed)}; the runtime reads it all the same, so translations are kept in a place the repository said they are not.");
        }
    }

    // A pack's every file is the pack's; a file the project embeds is the assembly's own or a satellite's by what the build did with it; a file in the project's own .locales folder that it does not embed is the host's.
    private static LocaleSources? GetSource(LoadedProject project, string fullPath, string hostFolder)
    {
        if (project.IsLocalePack)
        {
            return LocaleSources.Pack;
        }

        if (project.EmbeddedResourceNames!.ContainsKey(fullPath))
        {
            return project.SatelliteFiles?.Contains(fullPath) == true ? LocaleSources.Satellite : LocaleSources.Embedded;
        }

        return fullPath.StartsWith(hostFolder, StringComparison.OrdinalIgnoreCase) ? LocaleSources.Host : null;
    }

    private static string Describe(LocaleSources source)
        => source switch
        {
            LocaleSources.Embedded => "embedded in the assembly",
            LocaleSources.Satellite => "embedded in a satellite assembly",
            LocaleSources.Pack => "carried by a locale pack",
            _ => "laid in the host's folder",
        };
}
