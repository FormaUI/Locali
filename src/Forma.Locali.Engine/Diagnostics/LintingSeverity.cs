namespace Forma.Locali.Diagnostics;

/// <summary>How much a diagnostic matters.</summary>
/// <remarks>Declared in ascending order, so a comparison selects everything at a level and above.</remarks>
public enum LintingSeverity
{
    /// <summary>Something worth changing that still loads.</summary>
    Warning,

    /// <summary>Something the schema or the grammar forbids.</summary>
    Error,
}
