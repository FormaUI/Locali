namespace Forma.Locali.Rules;

using Forma.Framework.Localization.Parser.Syntax;
using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

/// <summary>What a call-site rule reads, and what it reports through.</summary>
/// <param name="site">The call site being checked.</param>
/// <param name="expression">The key's expressions, already parsed.</param>
public sealed class CallSiteContext(LocalizerCallSite site, LocaleParseResult expression)
{
    private readonly List<LintDiagnostic> _reported = [];
    private List<string>? _holes;

    /// <summary>The call site being checked.</summary>
    public LocalizerCallSite Site { get; } = site ?? throw new ArgumentNullException(nameof(site));

    /// <summary>The key's expressions, parsed once for every rule.</summary>
    public LocaleParseResult Expression { get; } = expression;

    /// <summary>The distinct names the key's holes read, in the order they first appear.</summary>
    public IReadOnlyList<string> Holes
        => _holes ??= HoleNames.Distinct(Site.Key);

    /// <summary>Everything the rules reported.</summary>
    public IReadOnlyList<LintDiagnostic> Reported
        => _reported;

    /// <summary>Reports against the key.</summary>
    /// <param name="code">The rule reporting.</param>
    /// <param name="message">What is wrong, in a sentence.</param>
    public void Report(LintCode code, string message)
    {
        ArgumentNullException.ThrowIfNull(code);

        _reported.Add(new LintDiagnostic(
            Site.Location.Path,
            Site.Location.StartLinePosition.Line + 1,
            Site.Location.StartLinePosition.Character + 1 + Site.KeyOffset,
            code.DefaultSeverity,
            code,
            message));
    }
}
