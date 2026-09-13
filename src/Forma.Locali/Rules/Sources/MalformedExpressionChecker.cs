namespace Forma.Locali.Rules.Sources;

using Forma.Locali.Diagnostics;

/// <summary>Checks that every expression a key carries follows the grammar.</summary>
public sealed class MalformedExpressionChecker : ICallSiteRuleChecker
{
    /// <inheritdoc />
    public LintingCode Code
        => LintingCodes.MalformedSourceExpression;

    /// <inheritdoc />
    public void Check(CallSiteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var diagnostic in context.Expression.Diagnostics)
        {
            context.Report(Code, $"The key's expression is malformed: {diagnostic.Kind}.");
        }
    }
}
