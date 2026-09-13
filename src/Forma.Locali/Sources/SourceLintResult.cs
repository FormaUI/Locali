namespace Forma.Locali.Sources;

using Forma.Locali.Diagnostics;

/// <summary>What a project's call sites produced, and the sites themselves for the rules that read a project whole.</summary>
/// <param name="Diagnostics">Everything the call-site rules reported.</param>
/// <param name="CallSites">The call sites the walk found, in the order it found them.</param>
public sealed record SourceLintResult(IReadOnlyList<LintingDiagnostic> Diagnostics, IReadOnlyList<LocalizerLookup> CallSites);
