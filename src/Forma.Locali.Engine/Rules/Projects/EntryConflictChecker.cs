namespace Forma.Locali.Rules.Projects;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

/// <summary>Checks that no two files of one source give an entry a type of the project reads different texts.</summary>
public sealed class EntryConflictChecker : IProjectRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.EntryConflict;

    /// <inheritdoc />
    public void Check(ProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Localization is not { } localization)
        {
            return;
        }

        // A conflict is met by every type reading the entry, and reported once for each culture it is met in.
        var met = new HashSet<(LocaleConflict Conflict, string CultureName)>();

        foreach (var culture in localization.Cultures)
        {
            // An application's own source reads its files for the assemblies their names start with as well, and meets their conflicts there.
            var conflicts = localization.TypeNames.SelectMany(typeName => localization.GetConflicts(typeName, culture)).Concat(localization.GetApplicationConflicts(culture));

            foreach (var conflict in conflicts)
            {
                // One file giving one name twice is a key worded two ways across its sections, which LOC0113 reports.
                if (conflict.FileName != conflict.OtherFileName && met.Add((conflict, culture.Name)))
                {
                    Report(context, localization, conflict, culture.Name);
                }
            }
        }
    }

    private void Report(ProjectContext context, ProjectLocalization localization, LocaleConflict conflict, string cultureName)
    {
        var path = localization.GetFile(conflict.OtherFileName)?.Path ?? conflict.OtherFileName;
        var other = localization.GetFile(conflict.FileName) is { } file && Path.GetDirectoryName(path) is { Length: > 0 } folder
            ? Path.GetRelativePath(folder, file.Path)
            : conflict.FileName;

        var reader = !conflict.IsShared
            ? $"'{conflict.TypeName}'"
            : conflict.TypeName.Length == 0 ? "every type outside the root namespace" : $"the types beneath '{conflict.TypeName}'";

        context.Report(
            Code,
            path,
            $"This file and '{other}' give {(conflict.IsShared ? "the shared entry " : string.Empty)}'{conflict.Key}' different texts for {reader} in '{cultureName}', and the runtime keeps the text of the file whose name sorts last.");
    }
}
