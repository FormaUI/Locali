namespace Forma.Locali.Flow;

using System.Collections.Concurrent;

using Forma.Locali.Configuration;
using Forma.Locali.Diagnostics;

using Microsoft.CodeAnalysis;

/// <summary>The member hints a run applies, each bound to the one declaration it names, with what is wrong in the rest and where the flow contradicts one.</summary>
internal sealed class LocaleMemberHints
{
    private readonly Dictionary<SymbolKey, LocaleMemberHint> _byMember = [];
    private readonly List<LintingDiagnostic> _diagnostics = [];
    private readonly ConcurrentDictionary<LocaleMemberHint, ConcurrentBag<string>> _contradictions = new();

    /// <summary>Binds each hint to the declaration it names in the run's compilations or the assemblies they reference.</summary>
    /// <param name="compilations">The run's compilations.</param>
    /// <param name="hints">Every hint the run's configuration names.</param>
    public LocaleMemberHints(IReadOnlyList<Compilation> compilations, IReadOnlyList<LocaleMemberHint> hints)
    {
        ArgumentNullException.ThrowIfNull(compilations);
        ArgumentNullException.ThrowIfNull(hints);

        var bound = new List<(SymbolKey Member, LocaleMemberHint Hint)>();
        foreach (var hint in hints)
        {
            if (Bind(compilations, hint) is { } member)
            {
                bound.Add((member, hint));
            }
        }

        // Hints repeating one relationship agree, and hints relating one member to different type members leave it to none.
        foreach (var group in bound.GroupBy(entry => entry.Member))
        {
            if (group.Select(entry => entry.Hint.TypeMember).Distinct(StringComparer.Ordinal).Count() == 1)
            {
                _byMember[group.Key] = group.First().Hint;
                continue;
            }

            foreach (var (_, hint) in group)
            {
                var other = group.First(entry => entry.Hint.TypeMember != hint.TypeMember).Hint;
                _diagnostics.Add(hint.Diagnose(
                    LintingCodes.ConflictingHint,
                    $"The hint relating '{hint.MemberName}' to '{hint.TypeMember}' disagrees with the one at '{other.Path}:{other.Line}', which relates it to '{other.TypeMember}', so neither applies."));
            }
        }
    }

    /// <summary>The hint written for a property's own declaration, or null when none is; an override or a member hiding it is a declaration of its own.</summary>
    /// <param name="property">The property.</param>
    public LocaleMemberHint? Of(IPropertySymbol property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return _byMember.Count == 0 ? null : _byMember.GetValueOrDefault(SymbolKey.Of(property.OriginalDefinition));
    }

    /// <summary>Records that the flow reaches another type than a hint reads, which sets the hint aside where the flow reaches it.</summary>
    /// <param name="hint">The hint.</param>
    /// <param name="message">What the hint and the flow each read, in a sentence.</param>
    public void Contradict(LocaleMemberHint hint, string message)
    {
        ArgumentNullException.ThrowIfNull(hint);
        ArgumentException.ThrowIfNullOrEmpty(message);

        _contradictions.GetOrAdd(hint, static _ => []).Add(message);
    }

    /// <summary>What is wrong in the hints, and for each the flow contradicts, the first contradiction by its message.</summary>
    public IReadOnlyList<LintingDiagnostic> GetDiagnostics()
        =>
        [
            .. _diagnostics,
            .. _contradictions.Select(entry => entry.Key.Diagnose(LintingCodes.ConflictingHint, entry.Value.Min(StringComparer.Ordinal)!)),
        ];

    private SymbolKey? Bind(IReadOnlyList<Compilation> compilations, LocaleMemberHint hint)
    {
        var assembly = compilations
            .Select(compilation => compilation.Assembly.Name == hint.Assembly
                ? compilation.Assembly
                : compilation.SourceModule.ReferencedAssemblySymbols.FirstOrDefault(referenced => referenced.Name == hint.Assembly))
            .FirstOrDefault(found => found is not null);
        if (assembly is null)
        {
            if (hint.IsHeldToRun)
            {
                _diagnostics.Add(hint.Diagnose(
                    LintingCodes.StaleConfigurationEntry,
                    $"'{hint.Assembly}' is no assembly a project of the run builds or references, so the hint for '{hint.MemberName}' applies to nothing."));
            }

            return null;
        }

        if (assembly.GetTypeByMetadataName(hint.Type) is not { } type)
        {
            _diagnostics.Add(hint.Diagnose(LintingCodes.InvalidHint, $"'{hint.Type}' is not a type '{hint.Assembly}' declares, so the hint relates nothing."));

            return null;
        }

        if (type.GetMembers(hint.Member) is not [IPropertySymbol { IsIndexer: false } property]
            || property.Type is not INamedTypeSymbol { MetadataName: "IStringLocale" } locale
            || locale.ContainingNamespace?.ToDisplayString() != "Forma.Framework.Localization")
        {
            _diagnostics.Add(hint.Diagnose(LintingCodes.InvalidHint, $"'{hint.Member}' is not a property of '{hint.Type}' returning IStringLocale, so the hint relates nothing."));

            return null;
        }

        // An instance reads an override or an implementation of an abstract or interface member, a declaration the hint was not written for.
        if (property.IsAbstract)
        {
            _diagnostics.Add(hint.Diagnose(
                LintingCodes.InvalidHint,
                $"'{hint.Member}' of '{hint.Type}' is abstract, so an instance reads an override the hint was not written for, and the hint relates nothing."));

            return null;
        }

        if (!IsTypeMember(type, hint.TypeMember))
        {
            _diagnostics.Add(hint.Diagnose(
                LintingCodes.InvalidHint,
                $"'{hint.TypeMember}' is not a property of '{hint.Type}', or of a type it derives from, returning Type, so the hint relates nothing."));

            return null;
        }

        return SymbolKey.Of(property);
    }

    private static bool IsTypeMember(INamedTypeSymbol type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.GetMembers(name).OfType<IPropertySymbol>().Any(property => !property.IsIndexer && property.Type.ToDisplayString() == "System.Type"))
            {
                return true;
            }
        }

        return false;
    }
}
