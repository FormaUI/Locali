namespace Forma.Locali.Diagnostics;

/// <summary>How many of one rule's diagnostics fell below the severity a run reports at.</summary>
/// <param name="Code">The rule they were reported under.</param>
/// <param name="Count">How many the rule contributed.</param>
public sealed record SuppressedRule(LintCode Code, int Count);
