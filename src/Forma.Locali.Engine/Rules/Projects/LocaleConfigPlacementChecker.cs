namespace Forma.Locali.Rules.Projects;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;

/// <summary>Checks that a project's <c>.locale.json</c> is embedded at the project's root and nowhere else, and that a locale pack carries none.</summary>
public sealed class LocaleConfigPlacementChecker : IProjectRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.MalformedLocaleConfig;

    // An assembly's config file is embedded at its project's root; the runtime reads one embedded elsewhere as the assembly's all the same, one in a host's folder not at all, and one in a pack not at all either.
    /// <inheritdoc />
    public void Check(ProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var projectFolder = Path.GetDirectoryName(Path.GetFullPath(context.Project.Path))!;

        foreach (var file in context.LocalizationFiles)
        {
            var fullPath = Path.GetFullPath(file);
            if (!Path.GetFileName(fullPath).Equals(LocaleConfig.FileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (context.Project.IsLocalePack)
            {
                context.Report(Code, fullPath, "A locale pack carries no config: the runtime lays each of its folders out by the root namespace the assembly it names declares in its own config, so this file steers nothing.");
            }
            else if (context.Project.EmbeddedResourceNames is { } resourceNames && !resourceNames.ContainsKey(fullPath))
            {
                context.Report(Code, fullPath, "The project does not embed this file, so the runtime never reads it, and a host's folder is never read for config; an assembly's config file is embedded at its project's root.");
            }
            else if (!Path.GetDirectoryName(fullPath)!.Equals(projectFolder, StringComparison.OrdinalIgnoreCase))
            {
                context.Report(Code, fullPath, $"This file sits beneath '{Path.GetRelativePath(projectFolder, Path.GetDirectoryName(fullPath)!)}'; an assembly's config file sits at its project's root, beside '{Path.GetFileName(context.Project.Path)}'.");
            }
        }
    }
}
