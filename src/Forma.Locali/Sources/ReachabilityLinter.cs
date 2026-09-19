namespace Forma.Locali.Sources;

using Forma.Framework.Localization.Files;
using Forma.Locali.Diagnostics;

/// <summary>
/// Checks that every translation a file declares is one some type reads. The runtime resolves a translation by a
/// type's full name, from the file's position and the sections inside it, so a type renamed or moved away, or a
/// section naming none, leaves translations unreachable while every other check stays green.
/// </summary>
public sealed class ReachabilityLinter : IReachabilityLinter
{
    /// <inheritdoc />
    public IReadOnlyList<LintingDiagnostic> Lint(ProjectLocalization localization)
    {
        ArgumentNullException.ThrowIfNull(localization);

        var reachable = GetReachable(localization);
        var diagnostics = new List<LintingDiagnostic>();

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
                diagnostics.Add(Diagnose(file, "No type reads this file: no locale path in force where it sits reads a file by its name."));
                continue;
            }

            if (read.Count == 0)
            {
                diagnostics.Add(Diagnose(file, $"No type reads this file: the runtime resolves translations by a type's full name, and neither {FormatNames(anchorNames, "nor")} nor any section in the file names one."));
                continue;
            }

            foreach (var section in unreachable)
            {
                diagnostics.Add(Diagnose(file, section.Length == 0
                    ? $"No type named {FormatNames(anchorNames, "or")} exists, so the entries outside any section are unreachable."
                    : section.StartsWith(LocalizationSchema.RootedSectionPrefix, StringComparison.Ordinal)
                        ? $"Section '{section}' names no type from the assembly's root, so its translations are unreachable."
                        : $"Section '{section}' names no type below this file's position, so its translations are unreachable."));
            }
        }

        return diagnostics;
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
                    if (localization.Source.GetFile(entry.FileName) is { } file)
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

    private static LintingDiagnostic Diagnose(ProjectLocaleFile file, string message)
        => new(file.Path, 1, 1, LintingCodes.UnreachableLocalization.DefaultSeverity, LintingCodes.UnreachableLocalization, message);
}
