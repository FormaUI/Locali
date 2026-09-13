namespace Forma.Locali.Linting;

using Forma.Locali.Diagnostics;
using Forma.Locali.Rules;

/// <summary>What one file's schema rules produced, and the rules its own tools entry leaves it out of.</summary>
/// <param name="Diagnostics">Every diagnostic the schema rules reported, unfiltered by what the file leaves out.</param>
/// <param name="Ignored">The rules the file's own tools entry leaves it out of.</param>
public sealed record LocalizationFileLintResult(IReadOnlyList<LintingDiagnostic> Diagnostics, IgnoredRules Ignored)
{
    /// <summary>A file that reported nothing and leaves nothing out, because it was never a localization file to begin with.</summary>
    public static LocalizationFileLintResult Empty { get; } = new([], IgnoredRules.None);
}
