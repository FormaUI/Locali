namespace Forma.Locali.Sources;

using Forma.Locali.Diagnostics;

/// <summary>Checks that no two files of one source stand for one anchor in one culture.</summary>
public interface IDuplicateAnchorFileLinter
{
    /// <summary>Every file that stands for an anchor and culture another file of the same source stands for too.</summary>
    /// <param name="files">One source's files, each with the anchors the runtime places it at.</param>
    IReadOnlyList<LintingDiagnostic> Lint(IReadOnlyList<PlacedLocaleFile> files);
}
