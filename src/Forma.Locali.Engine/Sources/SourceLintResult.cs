namespace Forma.Locali.Sources;

using Forma.Locali.Diagnostics;

/// <summary>What a project's callsites produced, and the sites themselves for the rules that read a project whole.</summary>
/// <param name="Diagnostics">Everything the callsite rules reported.</param>
/// <param name="Callsites">The callsites the walk found, in the order it found them.</param>
public sealed record SourceLintResult(IReadOnlyList<LintingDiagnostic> Diagnostics, IReadOnlyList<LocalizerLookup> Callsites)
{
    /// <summary>The lookups taking their key from a parameter of their own method, left for the calls of that method to write, whichever files the run named.</summary>
    public IReadOnlyList<ForwardedKey> ForwardedKeys { get; init; } = [];
}
