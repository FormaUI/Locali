namespace Forma.Locali.Diagnostics;

/// <summary>How many of one rule's diagnostics were left out, whether below the severity a run reports at or by a file's own configuration.</summary>
/// <param name="Code">The rule they were reported under.</param>
/// <param name="Count">How many the rule contributed.</param>
public sealed record SuppressedRule(LintingCode Code, int Count);
