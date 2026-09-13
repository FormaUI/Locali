namespace Forma.Locali.Rules;

using Forma.Framework.Localization.Parser.Syntax;
using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

/// <summary>What a call-site rule reads, and what it reports through.</summary>
/// <param name="lookup">The lookup being checked.</param>
/// <param name="expression">The key's expressions, already parsed.</param>
public sealed class CallSiteContext(LocalizerLookup lookup, LocaleParseResult expression)
{
    private readonly List<LintDiagnostic> _reported = [];
    private List<string>? _holes;

    /// <summary>The lookup being checked.</summary>
    public LocalizerLookup Lookup { get; } = lookup ?? throw new ArgumentNullException(nameof(lookup));

    /// <summary>The key's expressions, parsed once for every rule.</summary>
    public LocaleParseResult Expression { get; } = expression;

    /// <summary>The distinct names the key's holes read, in the order they first appear.</summary>
    public IReadOnlyList<string> Holes
        => _holes ??= HoleNames.Distinct(Lookup.Key.Text);

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
            Lookup.Key.Location.Path,
            Lookup.Key.Location.StartLinePosition.Line + 1,
            Lookup.Key.Location.StartLinePosition.Character + 1 + Lookup.Key.TextOffset,
            code.DefaultSeverity,
            code,
            message));
    }
}
