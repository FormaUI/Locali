namespace Forma.Locali.Flow;

using System.Collections.Concurrent;

using Forma.Locali.Projects;
using Forma.Locali.Sources;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

/// <summary>Follows the locale each lookup reads back to where the localization runtime decided its type, across the projects of a run.</summary>
public sealed class LocaleFlow
{
    private readonly FlowRun _run;
    private readonly ConcurrentDictionary<(SymbolKey Member, SymbolKey? Instance), IReadOnlyList<FlowValue>> _memo = new();

    /// <summary>Reads the run's projects.</summary>
    /// <param name="projects">Every project the run loaded.</param>
    /// <param name="typeMemberOf">The name of the member naming the type a locale member's locale is read for, or null when no relationship names one.</param>
    public LocaleFlow(IReadOnlyList<LoadedProject> projects, Func<ISymbol, string?> typeMemberOf)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(typeMemberOf);

        _run = new FlowRun(projects, typeMemberOf);
    }

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

        return [.. lookups.AsParallel().AsOrdered().Select(lookup => lookup.Receiver is null ? lookup : Resolve(compilation, lookup))];
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

        var evaluator = new FlowEvaluator(_run, _memo);
        var values = evaluator.EntryFrames(compilation, enclosing, 0).SelectMany(frame => evaluator.Evaluate(receiver, frame)).ToArray();
        if (values.Length == 0)
        {
            return lookup with { Receiver = null };
        }

        var origins = values
            .OfType<FlowValue.Locale>()
            .GroupBy(locale => (SymbolKey.Of(locale.Type), locale.IsApproximate))
            .Select(group => new LocaleOrigin(group.First().Type, group.First().Root, group.Key.IsApproximate))
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

        return lookup with { Receiver = null, Origins = origins, Gaps = gaps };
    }

    /// <summary>The name of the member a <c>[LocaleSource]</c> attribute on a member names, or null when it carries none.</summary>
    /// <param name="member">The member.</param>
    public static string? ReadLocaleSourceAttribute(ISymbol member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return member.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == LocalizerContract.SourceAttributeMetadataName)
            is { ConstructorArguments: [{ Value: string name }] }
            ? name
            : null;
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
