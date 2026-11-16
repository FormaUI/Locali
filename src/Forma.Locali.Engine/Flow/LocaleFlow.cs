namespace Forma.Locali.Flow;

using System.Collections.Concurrent;

using Forma.Locali.Configuration;
using Forma.Locali.Diagnostics;
using Forma.Locali.Projects;
using Forma.Locali.Sources;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

/// <summary>Follows the locale each lookup reads back to where the localization runtime decided its type, across the projects of a run.</summary>
public sealed class LocaleFlow
{
    private readonly FlowRun _run;
    private readonly ConcurrentDictionary<(SymbolKey Member, SymbolKey? Instance), IReadOnlyList<FlowValue>> _memo = new();
    private readonly ConcurrentDictionary<(SymbolKey Member, SymbolKey? Instance), IReadOnlyList<FlowValue>> _analysisMemo = new();

    /// <summary>Reads the run's projects and binds the hints its configuration names.</summary>
    /// <param name="projects">Every project the run loaded.</param>
    /// <param name="hints">Every member hint the run's configuration names.</param>
    public LocaleFlow(IReadOnlyList<LoadedProject> projects, IReadOnlyList<LocaleMemberHint> hints)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(hints);

        _run = new FlowRun(projects, hints);
    }

    /// <summary>What is wrong in the hints, and where the flow contradicts one, as far as the lookups resolved so far tell.</summary>
    public IReadOnlyList<LintingDiagnostic> GetDiagnostics()
        => _run.Hints.GetDiagnostics();

    /// <summary>The lookups a project's callsites made, each with the types its locale reads and the paths that ended before reaching one.</summary>
    /// <param name="project">The project, one of the run's.</param>
    /// <param name="lookups">The lookups its callsites made.</param>
    public IReadOnlyList<LocalizerLookup> Resolve(LoadedProject project, IReadOnlyList<LocalizerLookup> lookups)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(lookups);

        if (project.Compilation is not { } compilation)
        {
            return lookups;
        }

        // A lookup through Microsoft's localizer reads Forma's entries only where the run has Forma's adapter answer it.
        var usesStringLocalizer = lookups.Any(lookup => lookup.UsesStringLocalizer) && _run.UsesStringLocalizer;

        return [.. lookups
            .Where(lookup => !lookup.UsesStringLocalizer || usesStringLocalizer)
            .AsParallel()
            .AsOrdered()
            .Select(lookup => lookup.Receiver is null ? lookup : Resolve(compilation, lookup))];
    }

    private LocalizerLookup Resolve(Compilation compilation, LocalizerLookup lookup)
    {
        var node = lookup.Receiver!;
        var model = _run.ModelOf(compilation, node.SyntaxTree);
        var receiver = model.GetOperation(node) switch
        {
            IPropertyReferenceOperation indexer => indexer.Instance,
            IInvocationOperation invocation => invocation.Instance,
            _ => null,
        };

        if (receiver is null || Enclosing(model, node.SpanStart) is not { } enclosing)
        {
            return lookup with { Receiver = null, Gaps = [new LocaleGap(LocaleGapKind.Unsupported, node.ToString())] };
        }

        var evaluator = new FlowEvaluator(_run, _memo, _analysisMemo);
        var values = evaluator.EntryFrames(compilation, enclosing, 0).SelectMany(frame => evaluator.Evaluate(receiver, frame)).ToArray();
        if (values.Length == 0)
        {
            return lookup with { Receiver = null };
        }

        var (origins, gaps) = Settle(values);

        return lookup with { Receiver = null, Origins = origins, Gaps = gaps };
    }

    /// <summary>The lookups keys passed through parameters make where the calls of their methods write them as text, by the project holding each call, and the keys some call writes otherwise or no call of the run writes at all.</summary>
    /// <param name="keys">Every key the run's callsites take from a parameter, with the project holding it.</param>
    public ForwardedLookups Forward(IReadOnlyList<(LoadedProject Project, ForwardedKey Key)> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var byCall = new Dictionary<(string Project, FileLinePositionSpan Key), (LocalizerLookup Site, List<FlowValue> Values)>();
        var unforwarded = new List<ForwardedKey>();
        foreach (var (project, key) in keys)
        {
            if (project.Compilation is not { } compilation
                || _run.ModelOf(compilation, key.Lookup.SyntaxTree).GetOperation(key.Lookup) is not { } lookup
                || KeyParameter(lookup) is not { } parameter)
            {
                unforwarded.Add(key);
                continue;
            }

            var calls = new List<ForwardedCall>();
            if (!new FlowEvaluator(_run, _memo, _analysisMemo).Forward(lookup, parameter, calls))
            {
                unforwarded.Add(key);
            }

            // A call reaching several lookups is one lookup of its key, in every type they read.
            foreach (var call in calls)
            {
                if (SemanticCallsiteReader.Read(call.Call, call.Call.Arguments, call.KeyOrdinal) is not { } site || _run.ProjectOf(call.Compilation) is not { } caller)
                {
                    continue;
                }

                var entry = (caller.Path, site.Key.Location);
                if (!byCall.TryGetValue(entry, out var known))
                {
                    known = (site with { Arguments = LocalizerArguments.Unknown, IsFormatString = false }, []);
                    byCall[entry] = known;
                }

                known.Values.AddRange(call.Values);
            }
        }

        var byProject = byCall
            .GroupBy(entry => entry.Key.Project, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                IReadOnlyList<LocalizerLookup> (group) => [.. group.Select(entry => Settled(entry.Value.Site, entry.Value.Values))],
                StringComparer.OrdinalIgnoreCase);

        return new ForwardedLookups(byProject, unforwarded);
    }

    private static LocalizerLookup Settled(LocalizerLookup site, List<FlowValue> values)
    {
        var (origins, gaps) = Settle(values);

        return site with { Origins = origins, Gaps = gaps };
    }

    // Locales group into the types they read, and every other value is where a path ended.
    private static (LocaleOrigin[] Origins, LocaleGap[] Gaps) Settle(IReadOnlyList<FlowValue> values)
    {
        var origins = values
            .OfType<FlowValue.Locale>()
            .GroupBy(locale => (SymbolKey.Of(locale.Type), locale.IsApproximate, locale.Evidence, locale.Hint))
            .Select(group => new LocaleOrigin(group.First().Type, group.First().Root, group.Key.IsApproximate, group.Key.Evidence, group.Key.Hint))
            .ToArray();
        var gaps = values
            .Select(value => value switch
            {
                FlowValue.Gap gap => new LocaleGap(gap.Kind, gap.Detail),
                FlowValue.Locale => null,
                _ => new LocaleGap(LocaleGapKind.Unsupported, $"a value that is not a locale, {value.GetType().Name}"),
            })
            .OfType<LocaleGap>()
            .Distinct()
            .ToArray();

        return (origins, gaps);
    }

    // The parameter a lookup writes as its key, its first argument.
    private static IParameterSymbol? KeyParameter(IOperation lookup)
    {
        IOperation? key = lookup switch
        {
            IPropertyReferenceOperation indexer => indexer.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 0)?.Value,
            IInvocationOperation invocation => invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 0)?.Value,
            _ => null,
        };
        while (key is IConversionOperation conversion)
        {
            key = conversion.Operand;
        }

        return (key as IParameterReferenceOperation)?.Parameter;
    }

    // A lambda or a local function runs as part of the member holding it.
    private static ISymbol? Enclosing(SemanticModel model, int position)
    {
        var symbol = model.GetEnclosingSymbol(position);
        while (symbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction })
        {
            symbol = symbol.ContainingSymbol;
        }

        return symbol;
    }
}
