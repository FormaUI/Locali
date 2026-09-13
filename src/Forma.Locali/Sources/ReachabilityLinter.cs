namespace Forma.Locali.Sources;

using Forma.Locali.Diagnostics;

/// <summary>
/// Checks that every translation a file declares is one some type reads. The runtime resolves a translation by a
/// type's full name, from the file's position and the sections inside it, so a type renamed or moved away, or a
/// section naming none, leaves translations unreachable while every other check stays green.
/// </summary>
public sealed class ReachabilityLinter : IReachabilityLinter
{
    /// <inheritdoc />
    public IReadOnlyList<LintDiagnostic> Lint(ProjectTranslations translations)
    {
        ArgumentNullException.ThrowIfNull(translations);

        var reachable = GetReachable(translations);
        var diagnostics = new List<LintDiagnostic>();

        foreach (var file in translations.Files.Where(file => !translations.Source.IsContested(file)))
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

            if (read.Count == 0)
            {
                diagnostics.Add(Diagnose(file, $"No type reads this file: the runtime resolves translations by a type's full name, and neither '{file.ExpectedName}' nor any section in the file names one."));
                continue;
            }

            foreach (var section in unreachable)
            {
                diagnostics.Add(Diagnose(file, section.Length == 0
                    ? $"No type named '{file.ExpectedName}' exists, so the entries outside any section are unreachable."
                    : $"Section '{section}' names no type below this file's position, so its translations are unreachable."));
            }
        }

        return diagnostics;
    }

    // Every type's walk, in every culture the files carry, names each entry it read together with the file and section it came from.
    private static Dictionary<string, HashSet<(string TypeName, string Key)>> GetReachable(ProjectTranslations translations)
    {
        var reachable = new Dictionary<string, HashSet<(string TypeName, string Key)>>(StringComparer.Ordinal);

        foreach (var typeName in translations.TypeNames)
        {
            foreach (var culture in translations.Cultures)
            {
                foreach (var entry in translations.Read(typeName, culture))
                {
                    if (translations.Source.GetFile(entry.FileName) is { } file)
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

    private static LintDiagnostic Diagnose(TranslationFile file, string message)
        => new(file.Path, 1, 1, LintCodes.UnreachableTranslation.DefaultSeverity, LintCodes.UnreachableTranslation, message);
}
