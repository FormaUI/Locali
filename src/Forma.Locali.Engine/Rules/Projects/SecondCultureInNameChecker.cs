namespace Forma.Locali.Rules.Projects;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Documents;
using Forma.Locali.Sources;

/// <summary>Checks that no part of the name a project's locale file, or a pack's for it, is embedded under is a culture other than the file's own and names no type, where the runtime reads it into a name no type reads.</summary>
public sealed class SecondCultureInNameChecker : IProjectRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.SecondCultureInName;

    /// <inheritdoc />
    public void Check(ProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The types a part could name are known only where the project compiled.
        if (context.Localization is not { } localization)
        {
            return;
        }

        // The names a project embeds its files under are known only where its items were read.
        var embeddedFiles = context.Project.EmbeddedResourceNames is null ? [] : localization.EmbeddedFiles;

        foreach (var file in embeddedFiles)
        {
            // A part of an application's file may name a type of an assembly whose types are out of reach.
            if (localization.IsReadOutOfReach(file))
            {
                continue;
            }

            Report(context, localization, file, LocalizationFileName.Read(localization.GetOwnName(file.Name), localization.Format.Extension, localization.LocalesDir));
        }

        // A pack's file is read for the project whose assembly its name starts with, so its parts are held to that project's types.
        foreach (var pack in localization.Packs)
        {
            foreach (var file in pack.Files)
            {
                Report(context, localization, file, pack.Pack.FileNames.GetValueOrDefault(file.Path)?.FileName);
            }
        }
    }

    // The reading has left out the file's culture and every part repeating it, so a culture among the parts is another.
    private void Report(ProjectContext context, ProjectLocalization localization, ProjectLocaleFile file, LocaleFileName? fileName)
    {
        if (fileName?.Parts.FirstOrDefault(part => LocaleFileNames.IsCulture(part) && !localization.IsNameSegment(part)) is not { } part)
        {
            return;
        }

        context.Report(
            Code,
            file.Path,
            $"'{part}' in the name this file is embedded under is a culture other than the file's own '{fileName.Culture}', so the runtime reads the file for '{fileName.Culture}' with '{part}' as part of the name it stands for, which no type reads.");
    }
}
