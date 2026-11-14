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
            // An application's file whose name starts with an assembly's is read for that assembly's types too, which the project's own do not show.
            if (localization.IsApplication && localization.GetNamedAssemblies(file).Count > 0)
            {
                continue;
            }

            var read = reachable.GetValueOrDefault(file.Path) ?? [];
            var unreachable = file.File.GetEntries()
                .Select(entry => entry.TypeName)
                .Distinct(StringComparer.Ordinal)
                .Where(section => !read.Contains(section))
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

    // Every type's walk, in every culture the files carry, names each section it read, whether or not its entries win, together with the file it sits in.
    private static Dictionary<string, HashSet<string>> GetReachable(ProjectLocalization localization)
    {
        var reachable = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var typeName in localization.TypeNames)
        {
            foreach (var culture in localization.Cultures)
            {
                foreach (var section in localization.GetSections(typeName, culture))
                {
                    if (localization.GetFile(section.FileName) is { } file)
                    {
                        if (!reachable.TryGetValue(file.Path, out var sections))
                        {
                            reachable[file.Path] = sections = [with(StringComparer.Ordinal)];
                        }

                        sections.Add(section.TypeName);
                    }
                }
            }
        }

        return reachable;
    }

    private static string FormatNames(IReadOnlyList<string> names, string conjunction)
        => string.Join($" {conjunction} ", names.Select(name => $"'{name}'"));
}
