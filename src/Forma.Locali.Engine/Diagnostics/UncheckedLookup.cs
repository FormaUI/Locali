namespace Forma.Locali.Diagnostics;

/// <summary>A lookup the run could not check, where it sits and what kept it unchecked.</summary>
/// <param name="Path">The file holding the lookup's key.</param>
/// <param name="Line">The key's line, counted from one.</param>
/// <param name="Column">The key's column, counted from one.</param>
/// <param name="Reasons">What kept the lookup unchecked, each named as the coverage's reasons are.</param>
/// <param name="Explanation">What kept the lookup unchecked, in a sentence.</param>
public sealed record UncheckedLookup(string Path, int Line, int Column, IReadOnlyList<string> Reasons, string Explanation);
