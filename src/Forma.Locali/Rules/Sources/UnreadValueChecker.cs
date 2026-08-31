namespace Forma.Locali.Rules.Sources;

using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

/// <summary>Checks that every value a lookup names is read by the key.</summary>
public sealed class UnreadValueChecker : ICallSiteRuleChecker
{
    /// <inheritdoc />
    public LintCode Code
        => LintCodes.UnreadValue;

    /// <inheritdoc />
    public void Check(CallSiteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Site.Arguments is not LocalizerArguments.Named)
        {
            return;
        }

        var read = HoleNames.Filled(context.Site.Key);

        foreach (var name in context.Site.Names)
        {
            // The nameless value is what an expression naming no argument reads.
            if (name.Length != 0 && !read.Contains(name, StringComparer.Ordinal))
            {
                context.Report(Code, $"The lookup names '{name}', which the key does not read.");
            }
        }
    }
}
