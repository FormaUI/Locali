namespace Forma.Locali.Sources;

using Forma.Framework.Localization.Language;
using Forma.Framework.Localization.Parser;
using Forma.Framework.Localization.Parser.Syntax;
using Forma.Locali.Diagnostics;
using Forma.Locali.Projects;
using Forma.Locali.Rules;

using Microsoft.CodeAnalysis;

/// <summary>Finds a project's localization callsites, then lets every rule read each one.</summary>
/// <param name="finders">Finds callsites, asked in order for the first that can read a compilation.</param>
/// <param name="checkers">The rules to run over each callsite.</param>
public sealed class SourceLinter(
    IEnumerable<ILocalizerCallsiteFinder> finders,
    IEnumerable<ICallsiteRuleChecker> checkers) : ISourceLinter
{
    private static readonly LocaleKeywords Known = new(PluralExpressionEvaluator.Keyword);

    private readonly IReadOnlyList<ILocalizerCallsiteFinder> _finders = finders?.ToArray() ?? throw new ArgumentNullException(nameof(finders));
    private readonly IEnumerable<ICallsiteRuleChecker> _checkers = checkers ?? throw new ArgumentNullException(nameof(checkers));

    /// <inheritdoc />
    public SourceLintResult Lint(
        LoadedProject project,
        IReadOnlyCollection<string> files,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(files);

        var diagnostics = new List<LintingDiagnostic>();

        if (project.HasRazorMarkup && !project.HasGeneratedSources)
        {
            diagnostics.Add(new LintingDiagnostic(
                project.Path,
                1,
                1,
                LintingCodes.RazorNotAnalysed.DefaultSeverity,
                LintingCodes.RazorNotAnalysed,
                "The project carries Razor markup but no generated C# was found; build with EmitCompilerGeneratedFiles=true or its callsites go unchecked."));
        }

        if (project.Compilation is not { } compilation)
        {
            return new SourceLintResult(diagnostics, []);
        }

        var lookups = Walk(compilation, files, cancellationToken);

        // Each site parses its own key, so the rules run as wide as the walk that found them; a lookup through Microsoft's localizer
        // reads its key by that contract, which the rules do not read.
        diagnostics.AddRange(lookups.Lookups
            .Where(site => !site.UsesStringLocalizer)
            .AsParallel()
            .WithCancellation(cancellationToken)
            .SelectMany(Check));

        diagnostics.AddRange(lookups.UnreadableKeys.Select(Unreadable));
        diagnostics.AddRange(lookups.InterpolatedKeys.Select(Interpolated));

        return new SourceLintResult(diagnostics, lookups.Lookups) { ForwardedKeys = lookups.ForwardedKeys };
    }

    /// <inheritdoc />
    public IReadOnlyList<LintingDiagnostic> Lint(IReadOnlyList<LocalizerLookup> forwarded, IReadOnlyList<ForwardedKey> unforwarded)
    {
        ArgumentNullException.ThrowIfNull(forwarded);
        ArgumentNullException.ThrowIfNull(unforwarded);

        return
        [
            .. forwarded.SelectMany(Check),
            .. unforwarded.Select(key => new LintingDiagnostic(
                key.Key.Path,
                key.Key.StartLinePosition.Line + 1,
                key.Key.StartLinePosition.Character + 1,
                LintingCodes.KeyNotAnalysed.DefaultSeverity,
                LintingCodes.KeyNotAnalysed,
                $"The lookup's key is the parameter '{key.Parameter}', which a call writes as an expression or passes from outside the run, so no rule could check what it reads.")),
        ];
    }

    // Naming the blind spot keeps a run from reading as complete coverage when part of it was unreadable.
    private static LintingDiagnostic Unreadable(FileLinePositionSpan location)
        => new(
            location.Path,
            location.StartLinePosition.Line + 1,
            location.StartLinePosition.Character + 1,
            LintingCodes.KeyNotAnalysed.DefaultSeverity,
            LintingCodes.KeyNotAnalysed,
            "The lookup's key is an expression rather than text, so no rule could check what it reads.");

    private static LintingDiagnostic Interpolated(FileLinePositionSpan location)
        => new(
            location.Path,
            location.StartLinePosition.Line + 1,
            location.StartLinePosition.Character + 1,
            LintingCodes.InterpolatedKey.DefaultSeverity,
            LintingCodes.InterpolatedKey,
            "The lookup's key is an interpolated string, so the formatted text becomes the key and no entry matches it; write the key as a literal and pass the values positionally.");

    private LocalizerLookups Walk(Compilation compilation, IReadOnlyCollection<string> files, CancellationToken cancellationToken)
    {
        // The semantic finder is asked first, so a syntactic one answers only where the contract does not bind.
        if (_finders.FirstOrDefault(candidate => candidate.CanRead(compilation)) is not { } finder)
        {
            return LocalizerLookups.Empty;
        }

        // Each tree binds independently, and the caller orders what comes back.
        var found = compilation.SyntaxTrees
            .AsParallel()
            .WithCancellation(cancellationToken)
            .Select(tree => finder.Find(compilation, tree))
            .ToArray();

        // A forwarded key is kept whichever file holds it, since a call in a file the run named may write it.
        return new LocalizerLookups(
            [.. found.SelectMany(lookups => lookups.Lookups).Where(site => Wanted(site.Key.Location, files))],
            [.. found.SelectMany(lookups => lookups.UnreadableKeys).Where(location => Wanted(location, files))],
            [.. found.SelectMany(lookups => lookups.InterpolatedKeys).Where(location => Wanted(location, files))])
        {
            ForwardedKeys = [.. found.SelectMany(lookups => lookups.ForwardedKeys)],
        };
    }

    // A project compiles whole because binding needs it, so only the files the run named are reported on.
    private static bool Wanted(FileLinePositionSpan location, IReadOnlyCollection<string> files)
        => files.Contains(location.Path, StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<LintingDiagnostic> Check(LocalizerLookup site)
    {
        // The key parses once here rather than inside each rule that reads its expressions.
        var context = new CallsiteContext(site, LocaleExpressionParser.Parse(site.Key.Text, Known));

        foreach (var checker in _checkers)
        {
            checker.Check(context);
        }

        return context.Reported;
    }
}
