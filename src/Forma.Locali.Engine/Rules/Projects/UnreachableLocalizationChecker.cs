namespace Forma.Locali.Rules.Projects;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

/// <summary>
/// Checks that every translation a file declares is one some type reads. The runtime resolves a translation by a
/// type's full name, from the file's position and the sections inside it, so a type renamed or moved away, or a
/// section naming none, leaves translations unreachable while every other check stays green.
/// </summary>
public sealed class UnreachableLocalizationChecker : IProjectRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.UnreachableLocalization;

    /// <inheritdoc />
    public void Check(ProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Localization is not { } localization)
        {
            return;
        }

        var reachable = GetReachable(localization);

        // What the linting configuration leaves out is left to the caller to apply, so a run can still count and brief it.
        foreach (var file in localization.Files)
        {
            var read = reachable.GetValueOrDefault(file.Path) ?? [];
            var unreachable = file.File.GetEntries()
                .GroupBy(entry => entry.TypeName, StringComparer.Ordinal)
                .Where(section => !section.Any(entry => read.Contains((entry.TypeName, entry.Key))))
                .Select(section => section.Key)
                .ToList();

            if (unreachable.Count == 0)
            {
                continue;
            }

            var anchorNames = localization.GetAnchorNames(file);
            if (anchorNames.Count == 0)
            {
                context.Report(Code, file.Path, "No type reads this file: no locale path in force where it sits reads a file by its name.");
                continue;
            }

            if (read.Count == 0)
            {
                context.Report(Code, file.Path, $"No type reads this file: the runtime resolves translations by a type's full name, and neither {FormatNames(anchorNames, "nor")} nor any section in the file names one.");
                continue;
            }

            foreach (var section in unreachable)
            {
                context.Report(Code, file.Path, section.Length == 0
                    ? $"No type named {FormatNames(anchorNames, "or")} exists, so the entries outside any section are unreachable."
                    : section.StartsWith(LocalizationSchema.RootedSectionPrefix, StringComparison.Ordinal)
                        ? $"Section '{section}' names no type from the assembly's root, so its translations are unreachable."
                        : $"Section '{section}' names no type below this file's position, so its translations are unreachable.");
            }
        }
    }

    // Every type's walk, in every culture the files carry, names each entry it read together with the file and section it came from.
    private static Dictionary<string, HashSet<(string TypeName, string Key)>> GetReachable(ProjectLocalization localization)
    {
        var reachable = new Dictionary<string, HashSet<(string TypeName, string Key)>>(StringComparer.Ordinal);

        foreach (var typeName in localization.TypeNames)
        {
            foreach (var culture in localization.Cultures)
            {
                foreach (var entry in localization.Read(typeName, culture))
                {
                    if (localization.GetFile(entry.FileName) is { } file)
                    {
                        if (!reachable.TryGetValue(file.Path, out var entries))
                        {
                            reachable[file.Path] = entries = [];
                        }

                        entries.Add((entry.TypeName, entry.Key));
                    }
                }
            }
        }

        return reachable;
    }

    private static string FormatNames(IReadOnlyList<string> names, string conjunction)
        => string.Join($" {conjunction} ", names.Select(name => $"'{name}'"));
}
