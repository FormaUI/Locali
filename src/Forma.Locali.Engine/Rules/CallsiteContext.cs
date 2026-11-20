namespace Forma.Locali.Rules;

using Forma.Framework.Localization.Parser.Syntax;
using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

/// <summary>What a callsite rule reads, and what it reports through.</summary>
/// <param name="lookup">The lookup being checked.</param>
/// <param name="expression">The key's expressions, already parsed.</param>
public sealed class CallsiteContext(LocalizerLookup lookup, LocaleParseResult expression)
{
    private readonly List<LintingDiagnostic> _reported = [];
    private List<string>? _names;

    /// <summary>The lookup being checked.</summary>
    public LocalizerLookup Lookup { get; } = lookup ?? throw new ArgumentNullException(nameof(lookup));

    /// <summary>The key's expressions, parsed once for every rule.</summary>
    public LocaleParseResult Expression { get; } = expression;

    /// <summary>The distinct names the key's values pair with: the names its holes read and the arguments its expressions name.</summary>
    public IReadOnlyList<string> Names
        => _names ??= HoleNames.Filled(Lookup.Key.Text);

    /// <summary>The names the lookup supplies a value for: none where it supplies no value, every name the key pairs values with where it supplies them by position, and null where its overload is unknown.</summary>
    public IReadOnlyList<string>? SuppliedNames
        => Lookup.Arguments.Kind switch
        {
            LocalizerArgumentKind.None => [],
            LocalizerArgumentKind.Positional => Names,
            _ => null,
        };

    /// <summary>Everything the rules reported.</summary>
    public IReadOnlyList<LintingDiagnostic> Reported
        => _reported;

    /// <summary>Reports against the key.</summary>
    /// <param name="code">The rule reporting.</param>
    /// <param name="message">What is wrong, in a sentence.</param>
    public void Report(LintingCode code, string message)
    {
        ArgumentNullException.ThrowIfNull(code);

        _reported.Add(new LintingDiagnostic(
            Lookup.Key.Location.Path,
            Lookup.Key.Location.StartLinePosition.Line + 1,
            Lookup.Key.Location.StartLinePosition.Character + 1 + Lookup.Key.TextOffset,
            code.DefaultSeverity,
            code,
            message));
    }
}
