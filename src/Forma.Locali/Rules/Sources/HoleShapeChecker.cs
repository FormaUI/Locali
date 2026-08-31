namespace Forma.Locali.Rules.Sources;

using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

/// <summary>Checks that a key writes nothing shaped like a hole the runtime cannot read.</summary>
public sealed class HoleShapeChecker : ICallSiteRuleChecker
{
    /// <inheritdoc />
    public LintCode Code
        => LintCodes.HoleShapedText;

    /// <inheritdoc />
    public void Check(CallSiteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var suspect in HoleShapes.Suspect(context.Site.Key))
        {
            context.Report(
                Code,
                $"The key writes '{{{suspect}}}', which is not a name a value can be read by, so it renders as written; double the braces to keep it as text.");
        }
    }
}
