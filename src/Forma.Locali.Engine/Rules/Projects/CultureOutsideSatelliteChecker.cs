namespace Forma.Locali.Rules.Projects;

using Forma.Locali.Diagnostics;

/// <summary>Checks that no satellite assembly holds a project's locale file under a name whose culture is not the satellite's, where the runtime reads the file under no name.</summary>
public sealed class CultureOutsideSatelliteChecker : IProjectRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.CultureOutsideSatellite;

    /// <inheritdoc />
    public void Check(ProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var project = context.Project;
        foreach (var file in context.LocalizationFiles)
        {
            var fullPath = Path.GetFullPath(file);
            if (project.GetCultureOutsideSatellite(fullPath) is not { } nameCulture)
            {
                continue;
            }

            var satelliteCulture = project.SatelliteCultures![fullPath];
            var name = project.EmbeddedResourceNames![fullPath];

            context.Report(Code, fullPath, $"The '{satelliteCulture}' satellite assembly holds this file under '{name}', whose culture is '{nameCulture}', and the runtime reads no file a satellite holds under another culture's name, so none of its entries is read.");
        }
    }
}
