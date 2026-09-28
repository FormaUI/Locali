namespace Forma.Locali.Rules.Projects;

using Forma.Framework.Localization.Files;
using Forma.Locali.Configuration;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;
using Forma.Locali.Projects;
using Forma.Locali.Sources;

/// <summary>Checks that every locale file comes from a source the linting configuration in force over it allows, a pack's file by the project it localizes.</summary>
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

            // A pack's file is asked about where it stands in the project it localizes, whose line says where that assembly's translations may live.
            var target = GetPackTarget(context.Pack, fullPath);

            // The configuration is asked about every locale file before anything else, so an entry over a project whose items were not read still counts as one that governs a file.
            if (_lintingConfiguration.GetLocaleSources(target?.StandIn ?? fullPath) is not { } allowed)
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

            var line = target is { } packTarget
                ? $"the last localeSources line matching it as a file of {Path.GetFileName(packTarget.Project.Path)}, the project it localizes,"
                : "the last localeSources line matching it";

            context.Report(
                Code,
                fullPath,
                $"This file is {Describe(source)}, which {line} does not allow, naming {LocaleSourceNames.Format(allowed)}; the runtime reads it all the same, so translations are kept in a place the repository said they are not.");
        }
    }

    // The project a pack's file localizes, found by the folder beneath the pack it sits in, with the path the file stands for inside that project; null for a file outside every assembly's folder or one naming no project of the run.
    private static (LoadedProject Project, string StandIn)? GetPackTarget(LocalePack? pack, string fullPath)
    {
        if (pack is null)
        {
            return null;
        }

        var relative = Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(pack.Project.Path))!, fullPath);
        var separator = relative.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        if (separator <= 0)
        {
            return null;
        }

        var assemblyName = relative[..separator];
        if (pack.Folders.FirstOrDefault(folder => folder.AssemblyName == assemblyName)?.Target is not { } target)
        {
            return null;
        }

        return (target, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target.Path))!, relative[(separator + 1)..]));
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
