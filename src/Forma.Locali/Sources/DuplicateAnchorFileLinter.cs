namespace Forma.Locali.Sources;

using Forma.Locali.Diagnostics;

/// <summary>Checks that no two files of one source stand for one anchor in one culture.</summary>
public sealed class DuplicateAnchorFileLinter : IDuplicateAnchorFileLinter
{
    /// <inheritdoc />
    public IReadOnlyList<LintingDiagnostic> Lint(IReadOnlyList<PlacedLocaleFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var diagnostics = new List<LintingDiagnostic>();

        // A file placed at several anchors takes part at each of them, and is reported once per anchor it shares.
        var placements = files
            .SelectMany(file => file.AnchorNames.Select(anchorName => (file, anchorName)))
            .GroupBy(placement => (placement.anchorName, placement.file.CultureName), placement => placement.file);

        foreach (var placement in placements)
        {
            var (anchorName, cultureName) = placement.Key;
            var sharing = placement.DistinctBy(file => file.Path, StringComparer.Ordinal).ToList();
            if (sharing.Count < 2)
            {
                continue;
            }

            foreach (var file in sharing)
            {
                var others = string.Join(" and ", sharing.Where(other => other != file).Select(other => $"'{other.Path}'"));
                diagnostics.Add(new LintingDiagnostic(
                    file.Path,
                    1,
                    1,
                    LintingCodes.DuplicateAnchorFile.DefaultSeverity,
                    LintingCodes.DuplicateAnchorFile,
                    $"This file and {others} both stand for '{anchorName}' in '{cultureName}', so both are read and, where they share a key, the file the later path reads wins."));
            }
        }

        return diagnostics;
    }
}
