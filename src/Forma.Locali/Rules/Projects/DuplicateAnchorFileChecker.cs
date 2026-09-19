namespace Forma.Locali.Rules.Projects;

using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

/// <summary>Checks that no two files of one source stand for one anchor in one culture.</summary>
public sealed class DuplicateAnchorFileChecker : IProjectRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.DuplicateAnchorFile;

    /// <inheritdoc />
    public void Check(ProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The host's folder is a source of its own, checked first because placing the embedded files can meet a config the runtime refuses.
        Check(context, context.HostFiles);

        if (context.Localization is { } localization)
        {
            Check(context, localization.GetPlacedFiles());
        }
    }

    private void Check(ProjectContext context, IReadOnlyList<PlacedLocaleFile> files)
    {
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
                context.Report(
                    Code,
                    file.Path,
                    $"This file and {others} both stand for '{anchorName}' in '{cultureName}', so both are read and, where they share a key, the file the later path reads wins.");
            }
        }
    }
}
