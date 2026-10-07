namespace Forma.Locali.Rules;

using Forma.Locali.Diagnostics;

/// <summary>Checks one thing about a project's locale files read whole.</summary>
public interface IProjectRuleChecker
{
    /// <summary>The rule this checker reports under.</summary>
    LintingCode Code { get; }

    /// <summary>Reads the context and reports whatever it finds wrong.</summary>
    /// <param name="context">What the checker reads, and what it reports through.</param>
    void Check(ProjectContext context);
}
