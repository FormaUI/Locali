namespace Forma.Locali.Rules;

using Forma.Locali.Diagnostics;

/// <summary>Checks one thing about a localization file's metadata.</summary>
public interface ILocalizationFileRuleChecker
{
    /// <summary>The rule this checker reports under.</summary>
    LintingCode Code { get; }

    /// <summary>Reads the context and reports whatever it finds wrong.</summary>
    /// <param name="context">What the checker reads, and what it reports through.</param>
    void Check(LocalizationFileContext context);
}
