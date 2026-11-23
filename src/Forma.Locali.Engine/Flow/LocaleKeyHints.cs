namespace Forma.Locali.Flow;

using Forma.Locali.Configuration;
using Forma.Locali.Diagnostics;
using Forma.Locali.Sources;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

/// <summary>The key hints a run applies, each bound to the one method it names: held to the method's body where a project of the run builds it, and read as stated at each call of a method the run only references.</summary>
internal sealed class LocaleKeyHints
{
    private const string LocaleNamespace = "Forma.Framework.Localization";
    private const int MaxDepth = 4;

    private readonly Dictionary<SymbolKey, KeyedMethod> _compiled = [];
    private readonly HashSet<SymbolKey> _answered = [];
    private readonly List<LintingDiagnostic> _diagnostics = [];

    /// <summary>Binds each hint to the method it names, and holds a hint for a method the run builds to that method's body.</summary>
    /// <param name="run">What the flow reads across the run.</param>
    /// <param name="hints">Every key hint the run reads.</param>
    public LocaleKeyHints(FlowRun run, IReadOnlyList<LocaleKeyHint> hints)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(hints);

        var bound = new List<(SymbolKey Method, KeyedMethod Keyed)>();
        foreach (var hint in hints)
        {
            bound.AddRange(Bind(run.Compilations, hint).Select(keyed => (SymbolKey.Of(keyed.Method), keyed)));
        }

        foreach (var group in bound.GroupBy(entry => entry.Method))
        {
            // Hints repeating one relationship agree, and hints relating one method differently leave it to none.
            if (group.Select(entry => Relates(entry.Keyed.Hint)).Distinct().Count() > 1)
            {
                foreach (var (_, keyed) in group)
                {
                    var other = group.First(entry => Relates(entry.Keyed.Hint) != Relates(keyed.Hint)).Keyed.Hint;
                    _diagnostics.Add(keyed.Hint.Diagnose(
                        LintingCodes.ConflictingHint,
                        $"The hint for '{keyed.Hint.MethodName}', which looks '{keyed.Hint.Parameter}' up {Relation(keyed.Hint)}, disagrees with the one at '{other.Path}:{other.Line}', which looks '{other.Parameter}' up {Relation(other)}, so neither applies."));
                }

                continue;
            }

            var first = group.First().Keyed;
            if (run.FindSource(first.Method) is ({ } source, { } compilation))
            {
                // A method the run builds is read from its body, and the hint answers only for the calls the run cannot see.
                if (Contradiction(run, first, (IMethodSymbol)source, compilation) is { } reason)
                {
                    _diagnostics.Add(first.Hint.Diagnose(
                        LintingCodes.ConflictingHint,
                        $"The hint saying '{first.Hint.MethodName}' looks '{first.Hint.Parameter}' up {Relation(first.Hint)} disagrees with its body, which {reason}, so it applies to none of its calls."));
                }
                else
                {
                    _answered.Add(group.Key);
                }

                continue;
            }

            _compiled[group.Key] = first;
        }
    }

    /// <summary>Whether no hint binds a method the run only references.</summary>
    public bool IsEmpty
        => _compiled.Count == 0;

    /// <summary>The methods the run only references that a hint binds.</summary>
    public IReadOnlyCollection<KeyedMethod> Compiled
        => _compiled.Values;

    /// <summary>The hint bound to a method the run only references, or null where none is.</summary>
    /// <param name="method">The method, from any compilation of the run.</param>
    public KeyedMethod? Of(IMethodSymbol method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return _compiled.Count == 0 ? null : _compiled.GetValueOrDefault(SymbolKey.Of(method.OriginalDefinition));
    }

    /// <summary>Whether a hint its body bears out answers for the calls of a method the run builds that come from outside the run.</summary>
    /// <param name="method">The method, from any compilation of the run.</param>
    public bool Answers(IMethodSymbol method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return _answered.Count > 0 && _answered.Contains(SymbolKey.Of(method.OriginalDefinition));
    }

    /// <summary>What is wrong in the hints, and where a method's body contradicts one.</summary>
    public IReadOnlyList<LintingDiagnostic> GetDiagnostics()
        => _diagnostics;

    private static (string Parameter, string? Locale, string? TypeParameter) Relates(LocaleKeyHint hint)
        => (hint.Parameter, hint.Locale, hint.TypeParameter);

    private static string Relation(LocaleKeyHint hint)
        => hint.TypeParameter is { } typeParameter ? $"for the type '{typeParameter}' names" : $"in the locale '{hint.Locale}' holds";

    // A hint relates every overload of the method taking text as its parameter, and relates none where one cannot take it.
    private List<KeyedMethod> Bind(IReadOnlyList<Compilation> compilations, LocaleKeyHint hint)
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
                    $"'{hint.Assembly}' is no assembly a project of the run builds or references, so the hint for '{hint.MethodName}' applies to nothing."));
            }

            return [];
        }

        if (assembly.GetTypeByMetadataName(hint.Type) is not { } type)
        {
            _diagnostics.Add(hint.Diagnose(LintingCodes.InvalidHint, $"'{hint.Type}' is not a type '{hint.Assembly}' declares, so the hint relates nothing."));

            return [];
        }

        IMethodSymbol[] methods = [.. type.GetMembers(hint.Method)
            .OfType<IMethodSymbol>()
            .Where(method => method.MethodKind == MethodKind.Ordinary
                && method.Parameters.Any(parameter => parameter.Name == hint.Parameter && parameter.Type.SpecialType == SpecialType.System_String))];
        if (methods.Length == 0)
        {
            _diagnostics.Add(hint.Diagnose(LintingCodes.InvalidHint, $"'{hint.Method}' is not a method of '{hint.Type}' taking text as '{hint.Parameter}', so the hint relates nothing."));

            return [];
        }

        var keyed = new List<KeyedMethod>();
        foreach (var method in methods)
        {
            if (Bind(type, method, hint) is not { } bound)
            {
                return [];
            }

            keyed.Add(bound);
        }

        return keyed;
    }

    private KeyedMethod? Bind(INamedTypeSymbol type, IMethodSymbol method, LocaleKeyHint hint)
    {
        // A call may run an override, or an implementation the hint was not written for.
        if (method.IsVirtual || method.IsAbstract || method.IsOverride && !method.IsSealed)
        {
            _diagnostics.Add(hint.Diagnose(
                LintingCodes.InvalidHint,
                $"'{hint.Method}' of '{hint.Type}' can be overridden, so a call may run an override the hint was not written for, and the hint relates nothing."));

            return null;
        }

        var keyOrdinal = method.Parameters.First(parameter => parameter.Name == hint.Parameter).Ordinal;
        if (hint.TypeParameter is { } typeParameterName)
        {
            if (method.TypeParameters.FirstOrDefault(typeParameter => typeParameter.Name == typeParameterName) is not { } typeParameter)
            {
                _diagnostics.Add(hint.Diagnose(LintingCodes.InvalidHint, $"'{typeParameterName}' is not a type parameter of every '{hint.Method}' taking text as '{hint.Parameter}', so the hint relates nothing."));

                return null;
            }

            return new KeyedMethod(hint, method, keyOrdinal, null, null, typeParameter.Ordinal);
        }

        var localeName = hint.Locale!;
        if (method.Parameters.FirstOrDefault(parameter => parameter.Name == localeName) is { } localeParameter)
        {
            if (!IsLocale(localeParameter.Type))
            {
                _diagnostics.Add(hint.Diagnose(LintingCodes.InvalidHint, $"'{localeName}' is a parameter of '{hint.Method}' that is no IStringLocale, so the hint relates nothing."));

                return null;
            }

            return new KeyedMethod(hint, method, keyOrdinal, localeParameter.Ordinal, null, null);
        }

        if (LocaleMember(type, localeName) is not { } member || method.IsStatic && !member.IsStatic)
        {
            _diagnostics.Add(hint.Diagnose(
                LintingCodes.InvalidHint,
                $"'{localeName}' is neither a parameter of every '{hint.Method}' taking text as '{hint.Parameter}' nor a property or field of '{hint.Type}', or of a type it derives from, returning IStringLocale that the method can read, so the hint relates nothing."));

            return null;
        }

        return new KeyedMethod(hint, method, keyOrdinal, null, member, null);
    }

    // Each lookup of the key in the method's body is read as the run reads a call's: on every path a call's text does not decide, with
    // the key unchanged, in the locale the hint names; a key passed on to a method the run builds is held to that method's body in turn.
    private static string? Contradiction(FlowRun run, KeyedMethod keyed, IMethodSymbol method, Compilation compilation)
    {
        var relation = keyed switch
        {
            { TypeParameter: { } typeParameter } => new LocaleRelation(method.TypeParameters[typeParameter], null, null),
            { LocaleParameter: { } localeParameter } => new LocaleRelation(null, method.Parameters[localeParameter], null),
            _ => new LocaleRelation(null, null, SymbolKey.Of(keyed.LocaleMember!)),
        };
        var key = method.Parameters[keyed.KeyOrdinal];

        return Contradiction(run, method, compilation, key, relation, 0, []) is { } reason ? $"{reason}" : null;
    }

    private static string? Contradiction(FlowRun run, IMethodSymbol method, Compilation compilation, IParameterSymbol key, LocaleRelation relation, int depth, HashSet<SymbolKey> following)
    {
        if (compilation.GetTypeByMetadataName(LocalizerContract.MetadataName) is not { } contract || Body(run, method, compilation) is not { } body)
        {
            return "the run cannot read";
        }

        if (depth > MaxDepth || !following.Add(SymbolKey.Of(key)))
        {
            return $"passes '{key.Name}' on further than the run follows";
        }

        IOperation[] lookups = [.. body.DescendantsAndSelf().Where(operation => Is(KeyOf(operation, contract), key))];
        (IInvocationOperation Call, IMethodSymbol Method, Compilation Compilation, IParameterSymbol Key)[] passed =
        [
            .. body.DescendantsAndSelf()
                .OfType<IInvocationOperation>()
                .Where(call => !lookups.Contains(call))
                .SelectMany(call => call.Arguments
                    .Where(argument => argument.Parameter is { } parameter && Is(Unconverted(argument.Value), key))
                    .Select(argument => (Call: call, argument.Parameter!.Ordinal)))
                .Select(entry => (entry.Call, Source: run.FindSource(entry.Call.TargetMethod), entry.Ordinal))
                .Where(entry => entry.Source is ({ } source, _) && source is IMethodSymbol { MethodKind: MethodKind.Ordinary } target && !target.IsVirtual && !target.IsAbstract && !target.IsOverride)
                .Select(entry => (entry.Call, (IMethodSymbol)entry.Source!.Value.Symbol, entry.Source.Value.Compilation, ((IMethodSymbol)entry.Source.Value.Symbol).Parameters[entry.Ordinal])),
        ];

        if (lookups.Length == 0 && passed.Length == 0)
        {
            return $"looks '{key.Name}' up nowhere the run reads, neither as a lookup's key nor through a method of the run it passes it to";
        }

        foreach (var lookup in lookups)
        {
            if (!Reads(run, lookup, key))
            {
                return $"looks '{key.Name}' up only on paths its text decides, or after changing it, in a way the flow cannot tell";
            }

            if (!IsRelated(relation, lookup))
            {
                return $"looks '{key.Name}' up in another locale";
            }
        }

        foreach (var (call, target, targetCompilation, targetKey) in passed)
        {
            if (!Reads(run, call, key))
            {
                return $"passes '{key.Name}' on only on paths its text decides, or after changing it, in a way the flow cannot tell";
            }

            if (Carried(relation, call, target) is not { } carried)
            {
                return $"looks '{key.Name}' up in another locale";
            }

            if (Contradiction(run, target, targetCompilation, targetKey, carried, depth + 1, following) is { } reason)
            {
                return reason;
            }
        }

        return null;
    }

    private static bool Reads(FlowRun run, IOperation target, IParameterSymbol key)
        => run.GraphOf(target) is { } graph && KeyReachAnalysis.Of(graph, target, key, new Dictionary<int, CallValue>(), false).Reach == KeyReach.Reads;

    private static bool Is(IOperation? operation, ISymbol symbol)
        => operation is IParameterReferenceOperation { Parameter: var parameter } && SymbolEqualityComparer.Default.Equals(parameter, symbol);

    // What a call passing the key on carries the locale as into the method it calls: the type argument the type is passed as, the
    // parameter the locale is passed to, or, called on the same object, the same member; null where the call carries it no way.
    private static LocaleRelation? Carried(LocaleRelation relation, IInvocationOperation call, IMethodSymbol target)
    {
        if (relation.TypeParameter is { } typeParameter)
        {
            var index = call.TargetMethod.TypeArguments.IndexOf(typeParameter, 0, SymbolEqualityComparer.Default);

            return index < 0 ? null : new LocaleRelation(target.TypeParameters[index], null, null);
        }

        if (relation.Parameter is { } parameter)
        {
            return call.Arguments.FirstOrDefault(argument => Is(Unconverted(argument.Value), parameter))?.Parameter is { } passedTo
                ? new LocaleRelation(null, target.Parameters[passedTo.Ordinal], null)
                : null;
        }

        return call.Instance is null or IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance } ? relation : null;
    }

    // The key of a lookup the runtime makes: an indexer or a call to Resolve on a locale, or a call to LocalizableText.For.
    private static IOperation? KeyOf(IOperation operation, INamedTypeSymbol contract)
    {
        var (arguments, ordinal) = operation switch
        {
            IPropertyReferenceOperation { Arguments.Length: > 0 } indexer when LocalizerContract.Implements(indexer.Property.ContainingType, contract) => (indexer.Arguments, 0),
            IInvocationOperation { TargetMethod.Name: LocalizerContract.ResolveMethod } invocation when LocalizerContract.Implements(invocation.TargetMethod.ContainingType, contract) => (invocation.Arguments, 0),
            IInvocationOperation invocation when LocalizerContract.KeyOrdinal(invocation.TargetMethod) is { } keyOrdinal => (invocation.Arguments, keyOrdinal),
            _ => (default, -1),
        };

        return ordinal < 0 ? null : Unconverted(arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == ordinal)?.Value);
    }

    private static bool IsRelated(LocaleRelation relation, IOperation lookup)
    {
        if (relation.TypeParameter is { } typeParameter)
        {
            return lookup is IInvocationOperation { TargetMethod.TypeArguments: [var type] } invocation
                && LocalizerContract.KeyOrdinal(invocation.TargetMethod) is not null
                && SymbolEqualityComparer.Default.Equals(type, typeParameter);
        }

        var receiver = Unconverted(lookup switch
        {
            IPropertyReferenceOperation indexer => indexer.Instance,
            IInvocationOperation invocation => invocation.Instance,
            _ => null,
        });

        return relation.Parameter is { } parameter
            ? Is(receiver, parameter)
            : receiver switch
            {
                IPropertyReferenceOperation { Instance: null or IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance }, Property: var property }
                    => SymbolKey.Of(property).Equals(relation.Member),
                IFieldReferenceOperation { Instance: null or IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance }, Field: var field }
                    => SymbolKey.Of(field).Equals(relation.Member),
                _ => false,
            };
    }

    private static IOperation? Body(FlowRun run, IMethodSymbol method, Compilation compilation)
    {
        foreach (var declaration in method.DeclaringSyntaxReferences)
        {
            var node = declaration.GetSyntax();
            var model = run.ModelOf(compilation, node.SyntaxTree);
            if ((model.GetOperation(node) ?? (node.Parent is { } parent ? model.GetOperation(parent) : null)) is { } body)
            {
                return body;
            }
        }

        return null;
    }

    private static ISymbol? LocaleMember(INamedTypeSymbol type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers(name))
            {
                if (member is IPropertySymbol { IsIndexer: false, Type: var propertyType } && IsLocale(propertyType)
                    || member is IFieldSymbol { Type: var fieldType } && IsLocale(fieldType))
                {
                    return member;
                }
            }
        }

        return null;
    }

    private static bool IsLocale(ITypeSymbol type)
        => type is INamedTypeSymbol named
            && (IsLocaleContract(named) || named.AllInterfaces.Any(IsLocaleContract));

    private static bool IsLocaleContract(INamedTypeSymbol type)
        => type.OriginalDefinition.MetadataName is LocalizerContract.TypeName or $"{LocalizerContract.TypeName}`1"
            && type.ContainingNamespace?.ToDisplayString() == LocaleNamespace;

    private static IOperation? Unconverted(IOperation? operation)
    {
        while (operation is IConversionOperation { IsImplicit: true } conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    // Where a method's lookup of its key reads its locale: the type a type parameter names, the locale a parameter holds, or the locale
    // a member of the method's object holds; exactly one is set.
    private readonly record struct LocaleRelation(ITypeParameterSymbol? TypeParameter, IParameterSymbol? Parameter, SymbolKey? Member);
}
