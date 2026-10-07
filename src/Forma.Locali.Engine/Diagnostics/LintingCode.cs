namespace Forma.Locali.Diagnostics;

/// <summary>What a rule reports under, and how much it matters unless something overrides that.</summary>
/// <param name="Id">The identifier a build log and an editor show.</param>
/// <param name="DefaultSeverity">How much the rule matters when nothing says otherwise.</param>
/// <param name="Description">What the rule looks for, in a sentence.</param>
public sealed record LintingCode(string Id, LintingSeverity DefaultSeverity, string Description)
{
    /// <inheritdoc />
    public override string ToString()
        => Id;
}
