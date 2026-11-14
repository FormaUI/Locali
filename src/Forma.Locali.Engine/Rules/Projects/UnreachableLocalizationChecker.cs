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
            // An application's file read for an assembly whose types are out of reach may reach one of them.
            if (localization.IsReadOutOfReach(file))
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

            IReadOnlyList<string> anchorNames = [.. localization.GetAnchorNames(file), .. localization.GetApplicationAnchorNames(file)];
            if (anchorNames.Count == 0)
            {
                context.Report(Code, file.Path, "No type reads this file: no locale path in force where it sits reads a file by its name.");
                continue;
            }

            // A library's files are read for its own types alone, so a name starting with an assembly's is one only an application or a pack reads by.
            if (read.Count == 0 && !localization.IsApplication && localization.GetNamedAssemblies(file).MaxBy(assemblyName => assemblyName.Length) is { } assemblyName)
            {
                context.Report(Code, file.Path, $"No type reads this file: neither {FormatNames(anchorNames, "nor")} nor any section in the file names one, and its name starts with the assembly name '{assemblyName}', which only an application or a locale pack reads a file by, while this project is a library.");
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

        foreach (var culture in localization.Cultures)
        {
            // An application reads its files for the assemblies their names start with as well, so what those readings reach is reached.
            var read = localization.TypeNames.SelectMany(typeName => localization.GetSections(typeName, culture)).Concat(localization.GetApplicationSections(culture).Select(read => read.Section));

            foreach (var section in read)
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

        return reachable;
    }

    private static string FormatNames(IReadOnlyList<string> names, string conjunction)
        => string.Join($" {conjunction} ", names.Select(name => $"'{name}'"));
}
