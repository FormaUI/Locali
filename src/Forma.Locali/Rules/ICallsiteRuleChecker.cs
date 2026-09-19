namespace Forma.Locali.Rules;

using Forma.Locali.Diagnostics;

/// <summary>Checks one thing about a key written at a localization callsite.</summary>
public interface ICallsiteRuleChecker
{
    /// <summary>The rule this checker reports under.</summary>
    LintingCode Code { get; }

    /// <summary>Reads the context and reports whatever it finds wrong.</summary>
    /// <param name="context">What the checker reads, and what it reports through.</param>
    void Check(CallsiteContext context);
}
