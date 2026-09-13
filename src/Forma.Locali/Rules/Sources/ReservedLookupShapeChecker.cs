namespace Forma.Locali.Rules.Sources;

using Forma.Locali.Diagnostics;

/// <summary>Checks that a lookup does not write a key shaped like a reserved entry.</summary>
public sealed class ReservedLookupShapeChecker : ICallSiteRuleChecker
{
    /// <inheritdoc />
    public LintCode Code
        => LintCodes.ReservedShapedLookup;

    /// <inheritdoc />
    public void Check(CallSiteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (ReservedNames.IsShaped(context.Lookup.Key.Text))
        {
            context.Report(
                Code,
                $"The key '{context.Lookup.Key.Text}' is shaped like a reserved entry, which no file can answer; double the '$' to keep it as text.");
        }
    }
}
