namespace Forma.Locali.Flow;

using System.Collections.Concurrent;

using Forma.Locali.Configuration;
using Forma.Locali.Sources;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

using CSharp = Microsoft.CodeAnalysis.CSharp.Syntax;
using VisualBasic = Microsoft.CodeAnalysis.VisualBasic.Syntax;

/// <summary>Follows a value back through the code to where it was made: through locals, fields, properties, parameters, calls, lambdas and lazy values; one walk per lookup.</summary>
/// <param name="run">What the flow reads across the run.</param>
/// <param name="memo">The answers earlier walks completed, shared across lookups.</param>
/// <param name="analysisMemo">The answers earlier walks completed without hints, shared across lookups.</param>
internal sealed class FlowEvaluator(
    FlowRun run,
    ConcurrentDictionary<(SymbolKey Member, SymbolKey? Instance), IReadOnlyList<FlowValue>> memo,
    ConcurrentDictionary<(SymbolKey Member, SymbolKey? Instance), IReadOnlyList<FlowValue>> analysisMemo)
{
    private const int MaxDepth = 24;
    private const string LocaleFactoryMetadataName = "Forma.Framework.Localization.IStringLocaleFactory";
    private const string LocaleFactoryExtensionsMetadataName = "Forma.Framework.Localization.StringLocaleFactoryExtensions";
    private const string ServiceProviderExtensionsMetadataName = "Forma.Framework.Localization.ServiceProviderExtensions";
    private const string InjectAttributeName = "Microsoft.AspNetCore.Components.InjectAttribute";
    private const string ParameterAttributeName = "Microsoft.AspNetCore.Components.ParameterAttribute";
    private const string CascadingParameterAttributeName = "Microsoft.AspNetCore.Components.CascadingParameterAttribute";

    private readonly FlowRun _run = run ?? throw new ArgumentNullException(nameof(run));
    private readonly ConcurrentDictionary<(SymbolKey Member, SymbolKey? Instance), IReadOnlyList<FlowValue>> _memo = memo ?? throw new ArgumentNullException(nameof(memo));
    private readonly ConcurrentDictionary<(SymbolKey Member, SymbolKey? Instance), IReadOnlyList<FlowValue>> _analysisMemo = analysisMemo ?? throw new ArgumentNullException(nameof(analysisMemo));
    private readonly HashSet<(SymbolKey Member, SymbolKey? Instance, int Arguments)> _following = [];
    private readonly Dictionary<FlowValue.Allocated, Dictionary<SymbolKey, FlowFrame>> _constructorsRun = [with(ReferenceEqualityComparer.Instance)];

    // How deep the walk is in following a hinted member without its hint, where no hint applies and answers are kept apart.
    private int _verifying;

    private ConcurrentDictionary<(SymbolKey Member, SymbolKey? Instance), IReadOnlyList<FlowValue>> Memo
        => _verifying > 0 ? _analysisMemo : _memo;

    /// <summary>What an operation's value can be in a frame.</summary>
    /// <param name="operation">The operation.</param>
    /// <param name="frame">The frame it runs in.</param>
    public IReadOnlyList<FlowValue> Evaluate(IOperation? operation, FlowFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (operation is null)
        {
            return [];
        }

        if (frame.Depth > MaxDepth)
        {
            return [new FlowValue.Gap(LocaleGapKind.Depth, Describe(operation))];
        }

        // The generic contract names its type wherever the value came from, unless a conversion could have widened a locale for a type
        // derived from it, since the contract is covariant.
        if (operation is not IConversionOperation && operation.Type is { } valueType && Generic(valueType) is { } generic)
        {
            var declared = Locale(Named(generic, frame, $"IStringLocale<{generic.Name}>"));

            return declared.Any(IsWidenable) ? Widened(operation, frame, declared) : declared;
        }

        return Follow(operation, frame);
    }

    // A value whose declared type the run derives from is followed to where the runtime decided it; where its path leaves the run, or
    // nothing in the run gives it, the declared type is the contract the code outside keeps.
    private List<FlowValue> Widened(IOperation operation, FlowFrame frame, IReadOnlyList<FlowValue> declared)
    {
        var followed = Follow(operation, frame);
        var values = followed.Where(value => value is not FlowValue.Gap { Kind: LocaleGapKind.ExternalCallers or LocaleGapKind.CompiledCode }).ToList();

        return values.Count < followed.Count || followed.Count == 0 ? [.. values, .. declared] : values;
    }

    private bool IsWidenable(FlowValue value)
        => value is FlowValue.Locale { Type: var type }
            && (type.TypeKind == TypeKind.Interface || type.TypeKind == TypeKind.Class && !type.IsSealed && _run.HasDerived(type));

    private IReadOnlyList<FlowValue> Follow(IOperation operation, FlowFrame frame)
        => operation switch
        {
            IConversionOperation conversion => Evaluate(conversion.Operand, frame),
            IParenthesizedOperation parenthesized => Evaluate(parenthesized.Operand, frame),
            IDelegateCreationOperation creation => Evaluate(creation.Target, frame),
            IConditionalOperation { WhenFalse: { } whenFalse } conditional => Approximate([.. Evaluate(conditional.WhenTrue, frame), .. Evaluate(whenFalse, frame)]),
            ICoalesceOperation coalesce => Approximate([.. Evaluate(coalesce.Value, frame), .. Evaluate(coalesce.WhenNull, frame)]),
            ICoalesceAssignmentOperation assignment => EvaluateCoalesceAssignment(assignment, frame),
            ISimpleAssignmentOperation assignment => Evaluate(assignment.Value, frame),
            IInstanceReferenceOperation => frame.This is { } instance ? [instance] : [new FlowValue.Gap(LocaleGapKind.Unsupported, "an instance in a static member")],
            IConditionalAccessInstanceOperation access => Evaluate(ConditionalReceiver(access), frame),
            ILocalReferenceOperation local => EvaluateLocal(local, frame),
            IParameterReferenceOperation parameter => EvaluateParameter(parameter.Parameter, frame),
            IFieldReferenceOperation field => EvaluateMember(field.Field, field.Instance, frame),
            IPropertyReferenceOperation { Arguments.IsEmpty: true } property => EvaluateProperty(property, frame),
            IInvocationOperation invocation => EvaluateInvocation(invocation, frame),
            IObjectCreationOperation creation => EvaluateCreation(creation, frame),
            IAnonymousFunctionOperation or IMethodReferenceOperation => [new FlowValue.Function(operation, frame)],
            ITypeOfOperation typeOf => Named(typeOf.TypeOperand, frame, $"typeof({typeOf.TypeOperand.Name})"),
            ILiteralOperation { ConstantValue: { HasValue: true, Value: null } } or IDefaultValueOperation => [],
            _ => [new FlowValue.Gap(LocaleGapKind.Unsupported, Describe(operation))],
        };

    /// <summary>The values a member's body returns when entered with a frame.</summary>
    /// <param name="member">The method or accessor, from any compilation of the run.</param>
    /// <param name="frame">The frame to enter it with; its compilation is replaced by the one holding the member's source.</param>
    public IReadOnlyList<FlowValue> Returns(IMethodSymbol member, FlowFrame frame)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(frame);

        if (_run.FindSource(member) is not ({ } source, { } compilation) || source is not IMethodSymbol method)
        {
            return [new FlowValue.Gap(LocaleGapKind.CompiledCode, Name(member))];
        }

        // A body entered with no arguments on an instance of a known type gives the same answer however it was reached.
        SymbolKey? instanceKey = frame.This is FlowValue.Instance instance ? SymbolKey.Of(instance.Type) : null;
        var memoKey = (SymbolKey.Of(method), instanceKey);
        var memoizable = frame.Arguments.Count == 0 && frame.TypeArguments.Count == 0 && (frame.This is null or FlowValue.Instance)
            && method.MethodKind is not (MethodKind.LocalFunction or MethodKind.AnonymousFunction);
        if (memoizable && Memo.TryGetValue(memoKey, out var known))
        {
            return known;
        }

        var followKey = (memoKey.Item1, memoKey.instanceKey, ArgumentsKey(frame));
        if (!_following.Add(followKey))
        {
            return [new FlowValue.Gap(LocaleGapKind.Cycle, Name(method))];
        }

        try
        {
            var entered = new FlowFrame
            {
                Compilation = compilation,
                This = frame.This,
                Arguments = frame.Arguments,
                TypeArguments = frame.TypeArguments,
                Depth = frame.Depth + 1,
            };

            var values = new List<FlowValue>();
            var returns = Body(method, compilation) is { } body ? ReturnedValues(body).ToArray() : [];
            if (returns.Length == 0)
            {
                return [new FlowValue.Gap(LocaleGapKind.Unsupported, $"{Name(method)} returns nothing the flow reads")];
            }

            foreach (var returned in returns)
            {
                values.AddRange(Evaluate(returned, entered));
            }

            IReadOnlyList<FlowValue> result = returns.Length > 1 ? Approximate(values) : values;
            if (memoizable && !result.Any(IsCutShort))
            {
                Memo.TryAdd(memoKey, result);
            }

            return result;
        }
        finally
        {
            _following.Remove(followKey);
        }
    }

    // A frame by the code its arguments, and theirs, are written as, and the creation of the object it runs on, so a nested call with
    // other arguments is no cycle while a recursive one repeats within a few calls.
    private static int ArgumentsKey(FlowFrame frame, int levels = 2)
    {
        var hash = frame.This is FlowValue.Allocated { Creation: { } creation } ? creation.Syntax.GetHashCode() : 0;
        foreach (var (key, (value, argumentFrame)) in frame.Arguments)
        {
            hash ^= HashCode.Combine(key, value.Syntax, levels > 1 ? ArgumentsKey(argumentFrame, levels - 1) : 0);
        }

        return hash;
    }

    /// <summary>The types a type value names in a frame.</summary>
    /// <param name="type">The type, which may be a type parameter the frame binds.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="root">Where the value was written.</param>
    public static IReadOnlyList<FlowValue> Named(ITypeSymbol type, FlowFrame frame, string root)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(frame);

        return type switch
        {
            ITypeParameterSymbol parameter when frame.TypeArguments.TryGetValue(SymbolKey.Of(parameter), out var bound) => Named(bound, frame, root),
            INamedTypeSymbol { TypeKind: not TypeKind.Error } named => [new FlowValue.Named(named.OriginalDefinition, root)],
            _ => [new FlowValue.Gap(LocaleGapKind.Unsupported, $"the type {type.Name}")],
        };
    }

    /// <summary>The types an instance can be when the code runs on it: the declaring type unless abstract, and each class of the compilation deriving from it that leaves the member holding the code as it is.</summary>
    /// <param name="compilation">The compilation holding the code.</param>
    /// <param name="declaring">The type declaring the member.</param>
    /// <param name="enclosing">The member holding the code.</param>
    public IEnumerable<INamedTypeSymbol> Instances(Compilation compilation, INamedTypeSymbol declaring, ISymbol enclosing)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(declaring);
        ArgumentNullException.ThrowIfNull(enclosing);

        if (!declaring.IsAbstract)
        {
            yield return declaring;
        }

        foreach (var derived in _run.DerivedFrom(compilation, declaring.OriginalDefinition))
        {
            if (!derived.IsAbstract && !(enclosing is IMethodSymbol method && OverridesBetween(derived, declaring, method)))
            {
                yield return derived;
            }
        }
    }

    // A conditional access binds its receiver once, outside the access it guards.
    private static IOperation? ConditionalReceiver(IConditionalAccessInstanceOperation instance)
    {
        var access = instance.Parent;
        while (access is not null and not IConditionalAccessOperation)
        {
            access = access.Parent;
        }

        return (access as IConditionalAccessOperation)?.Operation;
    }

    // `base.`, and Visual Basic's `MyBase.` and `MyClass.`, call the member they name rather than the receiver's override.
    private static bool IsBaseReference(IOperation? instance)
        => instance is IInstanceReferenceOperation { Syntax: CSharp.BaseExpressionSyntax or VisualBasic.MyBaseExpressionSyntax or VisualBasic.MyClassExpressionSyntax };

    private static bool IsNull(IOperation? operation)
        => operation switch
        {
            null => true,
            IConversionOperation conversion => IsNull(conversion.Operand),
            ILiteralOperation { ConstantValue: { HasValue: true, Value: null } } or IDefaultValueOperation => true,
            _ => false,
        };

    private static bool IsCutShort(FlowValue value)
        => value is FlowValue.Gap { Kind: LocaleGapKind.Cycle or LocaleGapKind.Depth };

    // Alternatives naming one type, with no gap among them, pair with the lookup whichever is taken; any others, objects and
    // delegates among them, may not, so every value they hold is marked.
    private static IReadOnlyList<FlowValue> Approximate(IReadOnlyList<FlowValue> values)
    {
        var distinct = values.Distinct().ToArray();
        if (distinct.Length <= 1)
        {
            return values;
        }

        var types = distinct
            .Select(value => value switch
            {
                FlowValue.Locale locale => SymbolKey.Of(locale.Type),
                FlowValue.Named named => SymbolKey.Of(named.Type),
                _ => (SymbolKey?)null,
            })
            .ToArray();
        if (types.All(key => key is not null) && types.Distinct().Count() == 1)
        {
            return values;
        }

        return [.. values.Select(value => value with { IsApproximate = true })];
    }

    // What a value derives from an approximate one is approximate too.
    private static IReadOnlyList<FlowValue> From(FlowValue? source, IEnumerable<FlowValue> derived)
        => source is { IsApproximate: true } ? [.. derived.Select(value => value with { IsApproximate = true })] : [.. derived];

    // `field ??= value` caches its first value, so where nothing else writes the field the value is the one it holds.
    private IReadOnlyList<FlowValue> EvaluateCoalesceAssignment(ICoalesceAssignmentOperation assignment, FlowFrame frame)
    {
        var target = assignment.Target switch
        {
            IFieldReferenceOperation field => (ISymbol)field.Field,
            IPropertyReferenceOperation property => property.Property,
            _ => null,
        };

        return target is not null && FlowWrites.Of(_run, target).All(write => write.Kind is FlowWriteKind.LazyInitializer)
            ? Evaluate(assignment.Value, frame)
            : Approximate([.. Evaluate(assignment.Target, frame), .. Evaluate(assignment.Value, frame)]);
    }

    private IReadOnlyList<FlowValue> EvaluateLocal(ILocalReferenceOperation reference, FlowFrame frame)
    {
        IOperation top = reference;
        while (top.Parent is { } parent)
        {
            top = parent;
        }

        return FlowWrites.OfLocal(reference.Local, top) is [var single]
            ? Evaluate(single, frame)
            : [new FlowValue.Gap(LocaleGapKind.Local, reference.Local.Name)];
    }

    private IReadOnlyList<FlowValue> EvaluateParameter(IParameterSymbol parameter, FlowFrame frame)
    {
        var key = SymbolKey.Of(parameter);
        if (frame.Arguments.TryGetValue(key, out var bound))
        {
            return Evaluate(bound.Value, bound.Frame);
        }

        if (parameter.ContainingSymbol is not IMethodSymbol method || method.MethodKind is MethodKind.AnonymousFunction)
        {
            return [new FlowValue.Gap(LocaleGapKind.Unsupported, parameter.Name)];
        }

        // A constructor's parameter, a primary one's captured in a member among them, belongs to the object it built, so the creation
        // that ran the constructor, directly or through a chain, passes the value.
        if (method.MethodKind == MethodKind.Constructor && frame.This is FlowValue.Allocated { Creation: not null } allocated)
        {
            return ConstructorsRun(allocated).TryGetValue(SymbolKey.Of(method), out var constructorFrame)
                && constructorFrame.Arguments.TryGetValue(key, out var passed)
                    ? Evaluate(passed.Value, passed.Frame)
                    : [new FlowValue.Gap(LocaleGapKind.Unsupported, $"{parameter.Name} of a constructor {allocated.Type.Name}'s creation does not reach")];
        }

        return Callers(method, parameter, frame);
    }

    // A parameter reached without the call that entered its method takes the argument of every call the run makes to it, each a
    // path the code holding the lookup really runs on.
    private IReadOnlyList<FlowValue> Callers(IMethodSymbol method, IParameterSymbol parameter, FlowFrame frame)
    {
        var memoKey = (SymbolKey.Of(parameter), (SymbolKey?)null);
        if (Memo.TryGetValue(memoKey, out var known))
        {
            return known;
        }

        if (!_following.Add((memoKey.Item1, null, 0)))
        {
            return [new FlowValue.Gap(LocaleGapKind.Cycle, parameter.Name)];
        }

        try
        {
            var values = new List<FlowValue>();
            var compilation = frame.Compilation;
            if (IsReachableFromOutside(method) && compilation.Options.OutputKind == OutputKind.DynamicallyLinkedLibrary)
            {
                values.Add(new FlowValue.Gap(LocaleGapKind.ExternalCallers, $"{Name(method)}'s callers outside the run"));
            }

            foreach (var call in _run.CallsOf(method, compilation))
            {
                if (call is IMethodReferenceOperation)
                {
                    values.Add(new FlowValue.Gap(LocaleGapKind.ExternalCallers, $"{Name(method)} passed as a delegate"));
                    continue;
                }

                var arguments = call switch
                {
                    IInvocationOperation invocation => invocation.Arguments,
                    IObjectCreationOperation creation => creation.Arguments,
                    _ => [],
                };

                if (Enclosing(call, compilation) is not { } enclosing)
                {
                    continue;
                }

                foreach (var callerFrame in EntryFrames(compilation, enclosing, frame.Depth + 1))
                {
                    values.AddRange(Evaluate(ArgumentFor(arguments, parameter.Ordinal), callerFrame));
                }
            }

            if (values.Count == 0)
            {
                values.Add(new FlowValue.Gap(LocaleGapKind.ExternalCallers, $"no call to {Name(method)}"));
            }

            if (!values.Any(IsCutShort))
            {
                Memo.TryAdd(memoKey, values);
            }

            return values;
        }
        finally
        {
            _following.Remove((memoKey.Item1, null, 0));
        }
    }

    /// <summary>The frames the code of a member runs in when reached from no call: one for each type of instance it can run on, or one with no instance for a static member.</summary>
    /// <param name="compilation">The compilation holding the member's source.</param>
    /// <param name="enclosing">The member, a lambda's or a local function's own member.</param>
    /// <param name="depth">The depth the frames sit at.</param>
    public IEnumerable<FlowFrame> EntryFrames(Compilation compilation, ISymbol enclosing, int depth)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(enclosing);

        if (enclosing.IsStatic || enclosing.ContainingType is not { TypeKind: TypeKind.Class or TypeKind.Struct } declaring)
        {
            yield return new FlowFrame { Compilation = compilation, Depth = depth };
            yield break;
        }

        foreach (var instance in Instances(compilation, declaring, enclosing))
        {
            yield return new FlowFrame { Compilation = compilation, This = new FlowValue.Instance(instance), Depth = depth };
        }
    }

    private IReadOnlyList<FlowValue> EvaluateProperty(IPropertyReferenceOperation reference, FlowFrame frame)
    {
        var property = reference.Property;

        IReadOnlyList<FlowValue> receivers = reference.Instance is null ? [] : Evaluate(reference.Instance, frame);

        if (property.Name == "Value" && IsType(property.ContainingType, "System", "Lazy`1"))
        {
            return [.. receivers.SelectMany(receiver => receiver is FlowValue.Deferred deferred
                ? From(deferred, deferred.Producers.SelectMany(producer => Invoke(producer, [], frame)))
                : [Pass(receiver, property)])];
        }

        if (_verifying == 0 && _run.Hints.Of(property) is { } hint)
        {
            return [.. (reference.Instance is null ? [frame.This ?? new FlowValue.Gap(LocaleGapKind.Unsupported, property.Name)] : receivers)
                .SelectMany(receiver => From(receiver, Hinted(property, hint, reference.Instance, receiver, frame)))];
        }

        return EvaluateMember(property, reference.Instance, frame, receivers);
    }

    // A hint is held to the flow, followed without hints so that none vouches for itself or another: where the flow reaches the type
    // on its own the hint is verified or set aside, and elsewhere the type the hint relates the member to is the one read.
    private IReadOnlyList<FlowValue> Hinted(IPropertySymbol property, LocaleMemberHint hint, IOperation? instance, FlowValue receiver, FlowFrame frame)
    {
        if (receiver is not (FlowValue.Instance or FlowValue.Allocated))
        {
            return [Pass(receiver, property)];
        }

        // An instance overriding the member reads its own declaration, which the hint was not written for.
        var instanceType = receiver is FlowValue.Instance known ? known.Type : ((FlowValue.Allocated)receiver).Type;
        if (!IsBaseReference(instance) && !SymbolKey.Of(MostDerived(instanceType, property).OriginalDefinition).Equals(SymbolKey.Of(property.OriginalDefinition)))
        {
            return EvaluateMember(property, instance, frame, [receiver]);
        }

        IReadOnlyList<FlowValue> analysed;
        _verifying++;
        try
        {
            analysed = EvaluateMember(property, instance, frame, [receiver]);
        }
        finally
        {
            _verifying--;
        }

        var related = RelatedLocale(property, hint.TypeMember, receiver, frame);
        if (!IsSettled(analysed))
        {
            return Supplied(related, LocaleEvidence.Hint, hint);
        }

        if (!IsSettled(related))
        {
            return analysed;
        }

        if (!TypesOf(analysed).SetEquals(TypesOf(related)))
        {
            _run.Hints.Contradict(
                hint,
                $"The hint relating '{hint.MemberName}' to '{hint.TypeMember}' reads '{TypeNames(related)}' for '{instanceType.Name}', where the flow reaches '{TypeNames(analysed)}', so the flow's type is checked.");

            return analysed;
        }

        return Supplied(analysed, LocaleEvidence.VerifiedHint, hint);
    }

    // Locales of one certain type each, with nothing cut short or left unread among them.
    private static bool IsSettled(IReadOnlyList<FlowValue> values)
        => values.Count > 0 && values.All(value => value is FlowValue.Locale { IsApproximate: false });

    private static HashSet<SymbolKey> TypesOf(IReadOnlyList<FlowValue> values)
        => [.. values.OfType<FlowValue.Locale>().Select(locale => SymbolKey.Of(locale.Type))];

    private static string TypeNames(IReadOnlyList<FlowValue> values)
        => string.Join("', '", values.OfType<FlowValue.Locale>().Select(locale => locale.Type.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    private static IReadOnlyList<FlowValue> Supplied(IReadOnlyList<FlowValue> values, LocaleEvidence evidence, LocaleMemberHint hint)
        => [.. values.Select(value => value is FlowValue.Locale locale ? locale with { Evidence = evidence, Hint = hint } : value)];

    // A relationship says the member's locale is read for the type another member of the same instance returns.
    private IReadOnlyList<FlowValue> RelatedLocale(IPropertySymbol member, string typeMemberName, FlowValue receiver, FlowFrame frame)
    {
        if (receiver is not (FlowValue.Instance or FlowValue.Allocated))
        {
            return [Pass(receiver, member)];
        }

        var instanceType = receiver is FlowValue.Instance instance ? instance.Type : ((FlowValue.Allocated)receiver).Type;
        if (FindProperty(member.ContainingType, typeMemberName) is not { } typeMember)
        {
            return [new FlowValue.Gap(LocaleGapKind.UnreadTypeMember, $"{member.ContainingType.Name}.{typeMemberName} for {instanceType.Name}")];
        }

        // The instance reads the override its own type gives, so a getter that tells no type is named by that override.
        var read = MostDerived(instanceType, typeMember);
        var types = read.GetMethod is { } getter ? TypeMemberValues(getter, receiver, instanceType, frame) : [];
        if (types.Any(IsCutShort))
        {
            return [.. types.Where(IsCutShort)];
        }

        if (!types.Any(type => type is FlowValue.Named))
        {
            return [new FlowValue.Gap(LocaleGapKind.UnreadTypeMember, $"{read.ContainingType.Name}.{read.Name} for {instanceType.Name}")];
        }

        return Locale(types.Count > 1 ? Approximate(types) : types);
    }

    // A related type member is read in source where the run holds it, and otherwise in the compiled code of the assembly that runs.
    private IReadOnlyList<FlowValue> TypeMemberValues(IMethodSymbol getter, FlowValue receiver, INamedTypeSymbol instanceType, FlowFrame frame)
    {
        if (_run.FindSource(getter) is not null)
        {
            return Returns(getter, new FlowFrame { Compilation = frame.Compilation, This = receiver, Depth = frame.Depth });
        }

        foreach (var compilation in _run.Compilations)
        {
            if (compilation.GetMetadataReference(getter.ContainingAssembly) is not PortableExecutableReference { FilePath: { Length: > 0 } referencePath })
            {
                continue;
            }

            var runtimePath = _run.ProjectOf(compilation)?.RuntimeAssemblyPaths.GetValueOrDefault(Path.GetFullPath(referencePath));
            var returned = (runtimePath is not null ? CompiledGetters.Read(runtimePath, getter) : null) ?? CompiledGetters.Read(referencePath, getter);

            return returned switch
            {
                null => [],
                { Name: { } name } => FindType(compilation, getter.ContainingAssembly, name) is { } type ? [new FlowValue.Named(type.OriginalDefinition, $"typeof({type.Name})")] : [],
                _ => [new FlowValue.Named(instanceType.OriginalDefinition, $"GetType() of {instanceType.Name}")],
            };
        }

        return [];
    }

    private static INamedTypeSymbol? FindType(Compilation compilation, IAssemblySymbol getterAssembly, CompiledGetters.TypeName name)
    {
        var assembly = name.AssemblyName is null
            ? getterAssembly
            : compilation.SourceModule.ReferencedAssemblySymbols.FirstOrDefault(referenced => referenced.Name == name.AssemblyName);

        return assembly?.GetTypeByMetadataName(name.MetadataName) ?? compilation.GetTypeByMetadataName(name.MetadataName);
    }

    private IReadOnlyList<FlowValue> EvaluateMember(ISymbol member, IOperation? instance, FlowFrame frame, IReadOnlyList<FlowValue>? receivers = null)
    {
        var memberType = member switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null,
        };

        // A component's parameters and injected services are set by the framework, which a few contracts make known.
        if (HasAttribute(member, InjectAttributeName))
        {
            return Injected(memberType, member);
        }

        if (HasAttribute(member, ParameterAttributeName) || HasAttribute(member, CascadingParameterAttributeName))
        {
            return [new FlowValue.Gap(LocaleGapKind.Reflection, member.Name)];
        }

        receivers ??= instance is null
            ? member.IsStatic ? [] : [frame.This ?? new FlowValue.Gap(LocaleGapKind.Unsupported, member.Name)]
            : Evaluate(instance, frame);

        if (member.IsStatic)
        {
            return Stored(member, null, frame);
        }

        var nonVirtual = IsBaseReference(instance);

        return [.. receivers.SelectMany(receiver => receiver is FlowValue.Instance or FlowValue.Allocated ? From(receiver, ReadOn(member, receiver, frame, nonVirtual)) : [Pass(receiver, member)])];
    }

    // A property with a body runs it on the receiver, as the receiver's type overrides it; a field or an auto-property holds what
    // was written to it for that receiver.
    private IReadOnlyList<FlowValue> ReadOn(ISymbol member, FlowValue receiver, FlowFrame frame, bool nonVirtual)
    {
        var receiverType = receiver is FlowValue.Instance instance ? instance.Type : ((FlowValue.Allocated)receiver).Type;
        var resolved = member is IPropertySymbol property && !nonVirtual ? MostDerived(receiverType, property) : member;

        if (resolved is IPropertySymbol { GetMethod: { } getter } resolvedProperty)
        {
            if (_run.FindSource(getter) is ({ } source, { } compilation) && Body((IMethodSymbol)source, compilation) is not null)
            {
                return Returns(getter, new FlowFrame { Compilation = frame.Compilation, This = receiver, Depth = frame.Depth });
            }

            if (_run.FindSource(resolvedProperty) is null)
            {
                return [new FlowValue.Gap(LocaleGapKind.CompiledCode, Name(resolved))];
            }
        }

        return Stored(resolved, receiver, frame);
    }

    private List<FlowValue> Stored(ISymbol member, FlowValue? receiver, FlowFrame frame)
    {
        if (_run.FindSource(member) is not ({ } source, { } compilation))
        {
            return [new FlowValue.Gap(LocaleGapKind.CompiledCode, Name(member))];
        }

        // A null placeholder, as `= default!` is, holds nothing a lookup could read.
        var writes = FlowWrites.Of(_run, source).Where(write => !(write.Kind == FlowWriteKind.Initializer && IsNull(write.Value))).ToArray();
        if (writes.Any(write => write.Kind == FlowWriteKind.Outside))
        {
            return [new FlowValue.Gap(LocaleGapKind.Write, $"{Name(source)} is written outside its type")];
        }

        var constructorsRun = receiver is FlowValue.Allocated { Creation: not null } allocated ? ConstructorsRun(allocated) : null;
        var values = new List<FlowValue>();

        foreach (var write in writes)
        {
            var writeFrame = new FlowFrame { Compilation = compilation, This = receiver, Depth = frame.Depth + 1 };
            switch (write.Kind)
            {
                case FlowWriteKind.Initializer:
                case FlowWriteKind.LazyInitializer:
                    values.AddRange(Evaluate(write.Value, writeFrame));
                    break;
                case FlowWriteKind.Constructor when constructorsRun is null:
                    values.AddRange(Evaluate(write.Value, writeFrame));
                    break;
                case FlowWriteKind.Constructor when constructorsRun!.TryGetValue(SymbolKey.Of(write.Owner!), out var constructorFrame):
                    values.AddRange(Evaluate(write.Value, constructorFrame));
                    break;
                case FlowWriteKind.Constructor:
                    break;
                case FlowWriteKind.Other when writes.Length == 1 && write.Value is not null && PairsWith(write.Value, receiver):
                    // A field written once, and nowhere else, holds that value whenever the lookup can read it; a static one holds one value
                    // at a time.
                    values.AddRange(receiver is null ? Approximate(Evaluate(write.Value, writeFrame)) : Evaluate(write.Value, writeFrame));
                    break;
                default:
                    values.Add(new FlowValue.Gap(LocaleGapKind.Write, $"{Name(source)} is written in {(write.Owner is { } owner ? Name(owner) : "an initializer")}"));
                    break;
            }
        }

        if (values.Count == 0)
        {
            values.Add(new FlowValue.Gap(LocaleGapKind.Write, $"{Name(source)} is never written"));
        }

        // A constructor's write may replace an initializer's, and a later write a constructor's, so neither is certain beside the other.
        var shadowed = writes.Any(write => write.Kind == FlowWriteKind.Initializer) && writes.Any(write => write.Kind == FlowWriteKind.Constructor);

        return shadowed || values.Any(value => value is FlowValue.Gap { Kind: LocaleGapKind.Write }) ? [.. Approximate(values)] : values;
    }

    // A single write outside a constructor pairs with the object read where its value depends on no parameter or local, which a call
    // writing another object would bind otherwise; on the instance the code runs on, every object is a path the lookup really reads.
    private static bool PairsWith(IOperation value, FlowValue? receiver)
        => receiver is not FlowValue.Allocated
            || !value.DescendantsAndSelf().Any(operation => operation is IParameterReferenceOperation or ILocalReferenceOperation);

    // An object's constructor runs with the creation's arguments, and each constructor it chains to with the arguments the chain passes.
    private Dictionary<SymbolKey, FlowFrame> ConstructorsRun(FlowValue.Allocated allocated)
    {
        if (_constructorsRun.TryGetValue(allocated, out var known))
        {
            return known;
        }

        var run = new Dictionary<SymbolKey, FlowFrame>();
        _constructorsRun[allocated] = run;
        if (allocated is not { Creation: { Constructor: { } constructor } creation, Frame: { } creationFrame })
        {
            return run;
        }

        IMethodSymbol? current = constructor;
        IReadOnlyList<IArgumentOperation> arguments = creation.Arguments;
        var callerFrame = creationFrame;

        while (current is not null && _run.FindSource(current) is ({ } source, { } compilation) && source is IMethodSymbol sourceConstructor)
        {
            var frame = new FlowFrame
            {
                Compilation = compilation,
                This = allocated,
                Arguments = Bind(sourceConstructor, arguments, callerFrame),
                Depth = callerFrame.Depth + 1,
            };
            run[SymbolKey.Of(sourceConstructor)] = frame;

            var chained = (Body(sourceConstructor, compilation)?.DescendantsAndSelf() ?? PrimaryBase(sourceConstructor, compilation))
                .OfType<IInvocationOperation>()
                .FirstOrDefault(invocation => invocation.TargetMethod.MethodKind == MethodKind.Constructor && invocation.Instance is IInstanceReferenceOperation);
            current = chained?.TargetMethod;
            arguments = chained is null ? [] : chained.Arguments;
            callerFrame = frame;
        }

        return run;
    }

    // A primary constructor has no body; it chains through the base type its declaration's base list calls.
    private IEnumerable<IOperation> PrimaryBase(IMethodSymbol constructor, Compilation compilation)
    {
        foreach (var declaration in constructor.DeclaringSyntaxReferences)
        {
            var node = declaration.GetSyntax();
            if (node is not CSharp.TypeDeclarationSyntax { BaseList: { } baseList })
            {
                continue;
            }

            var model = _run.ModelOf(compilation, node.SyntaxTree);
            foreach (var baseType in baseList.Types.OfType<CSharp.PrimaryConstructorBaseTypeSyntax>())
            {
                if (model.GetOperation(baseType) is { } chained)
                {
                    yield return chained;
                }
            }
        }
    }

    private IReadOnlyList<FlowValue> EvaluateInvocation(IInvocationOperation invocation, FlowFrame frame)
    {
        var method = invocation.TargetMethod;
        var containing = method.ContainingType?.OriginalDefinition.ToDisplayString();

        // The localization runtime's own calls: a factory's Create decides a locale's type, and WithCulture keeps it.
        if (method.Name == "Create" && (containing == LocaleFactoryExtensionsMetadataName || Implements(method.ContainingType, LocaleFactoryMetadataName)))
        {
            return CreateLocale(invocation, frame);
        }

        if (containing == ServiceProviderExtensionsMetadataName && method.Name == "GetStringLocaleFactory")
        {
            return [new FlowValue.Factory(true, "GetStringLocaleFactory()")];
        }

        if (method.Name == "WithCulture" && Implements(method.ContainingType, LocalizerContract.MetadataName))
        {
            return Evaluate(invocation.Instance, frame);
        }

        if (method.Name is "GetService" or "GetRequiredService" && Requested(invocation) is { } requested)
        {
            return Injected(requested, method);
        }

        if (method is { Name: nameof(GetType), Parameters.IsEmpty: true, ContainingType.SpecialType: SpecialType.System_Object })
        {
            IReadOnlyList<FlowValue> instances = invocation.Instance is null ? frame.This is { } self ? [self] : [] : Evaluate(invocation.Instance, frame);

            return [.. instances.Select(value => value switch
            {
                FlowValue.Instance instance => new FlowValue.Named(instance.Type.OriginalDefinition, $"GetType() of {instance.Type.Name}") { IsApproximate = instance.IsApproximate },
                FlowValue.Allocated allocated => new FlowValue.Named(allocated.Type.OriginalDefinition, $"GetType() of {allocated.Type.Name}") { IsApproximate = allocated.IsApproximate },
                FlowValue.Gap gap => gap,
                _ => (FlowValue)new FlowValue.Gap(LocaleGapKind.Unsupported, "GetType()"),
            })];
        }

        if (method.MethodKind == MethodKind.DelegateInvoke)
        {
            return [.. Evaluate(invocation.Instance, frame).SelectMany(function => From(function, Invoke(function, invocation.Arguments, frame)))];
        }

        // A local function runs in the frame that called it, with every argument and capture that frame holds.
        if (method.MethodKind == MethodKind.LocalFunction)
        {
            return Returns(method, Enclosed(frame, method, invocation.Arguments, frame));
        }

        // A call runs the body its receiver's type gives the method, with the call's arguments and type arguments.
        var nonVirtual = IsBaseReference(invocation.Instance);
        IReadOnlyList<FlowValue?> receivers = invocation.Instance is null || method.IsStatic ? [null] : [.. Evaluate(invocation.Instance, frame)];
        var values = new List<FlowValue>();
        foreach (var receiver in receivers)
        {
            if (receiver is FlowValue.Gap gap)
            {
                values.Add(gap);
                continue;
            }

            if (Dispatch(method, receiver, nonVirtual) is not { } target)
            {
                values.Add(new FlowValue.Gap(LocaleGapKind.Dispatch, Name(method)));
                continue;
            }

            var (dispatched, instance) = target;
            values.AddRange(From(receiver, Returns(dispatched, new FlowFrame
            {
                Compilation = frame.Compilation,
                This = instance,
                Arguments = _run.FindSource(dispatched) is ({ } source, _) && source is IMethodSymbol sourceMethod ? Bind(sourceMethod, invocation.Arguments, frame) : [],
                TypeArguments = BindTypes(dispatched, method, frame),
                Depth = frame.Depth,
            })));
        }

        return values;
    }

    // A local function sees the frame it was declared in: its instance, its arguments, its type arguments, and its own parameters bound
    // to the call's arguments.
    private FlowFrame Enclosed(FlowFrame closure, IMethodSymbol localFunction, IReadOnlyList<IArgumentOperation> arguments, FlowFrame callerFrame)
    {
        var bound = new Dictionary<SymbolKey, (IOperation, FlowFrame)>(closure.Arguments);
        if (_run.FindSource(localFunction) is ({ } source, _) && source is IMethodSymbol sourceFunction)
        {
            foreach (var (key, value) in Bind(sourceFunction, arguments, callerFrame))
            {
                bound[key] = value;
            }
        }

        return new FlowFrame { Compilation = closure.Compilation, This = closure.This, Arguments = bound, TypeArguments = closure.TypeArguments, Depth = closure.Depth };
    }

    // The registered factory reads the files the run reads; any other factory may read other files, so its locale is not followed.
    private List<FlowValue> CreateLocale(IInvocationOperation invocation, FlowFrame frame)
    {
        var method = invocation.TargetMethod;
        var isExtension = method.IsStatic;
        var typeArgument = invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Type.ToDisplayString() == "System.Type");
        if (typeArgument is null)
        {
            return [new FlowValue.Gap(LocaleGapKind.NamedByString, "Create(baseName, location)")];
        }

        var factory = isExtension ? Evaluate(invocation.Arguments[0].Value, frame) : Evaluate(invocation.Instance, frame);
        var values = new List<FlowValue>();
        foreach (var source in factory)
        {
            switch (source)
            {
                case FlowValue.Factory { IsRegistered: true }:
                case FlowValue.Service service when service.Interface.ToDisplayString() == LocaleFactoryMetadataName:
                    values.AddRange(Locale(Evaluate(typeArgument.Value, frame)));
                    break;
                case FlowValue.Gap gap:
                    values.Add(gap);
                    break;
                default:
                    values.Add(new FlowValue.Gap(LocaleGapKind.CustomFactory, Describe(invocation)));
                    break;
            }
        }

        return values;
    }

    private IReadOnlyList<FlowValue> EvaluateCreation(IObjectCreationOperation creation, FlowFrame frame)
    {
        if (creation.Type is not INamedTypeSymbol type)
        {
            return [new FlowValue.Gap(LocaleGapKind.Unsupported, Describe(creation))];
        }

        if (IsType(type, "System", "Lazy`1"))
        {
            return creation.Arguments.FirstOrDefault() is { } factory
                ? [new FlowValue.Deferred(Evaluate(factory.Value, frame))]
                : [new FlowValue.Gap(LocaleGapKind.Unsupported, "a lazy value with no factory")];
        }

        if (Implements(type, LocaleFactoryMetadataName))
        {
            return [new FlowValue.Factory(false, type.Name)];
        }

        if (Implements(type, LocalizerContract.MetadataName))
        {
            return [new FlowValue.Gap(LocaleGapKind.Unsupported, $"a locale wrapper, {type.Name}")];
        }

        return [new FlowValue.Allocated(type.OriginalDefinition, creation, frame)];
    }

    // A delegate returns what its lambda, local function or method returns, entered with the invocation's arguments.
    private IReadOnlyList<FlowValue> Invoke(FlowValue function, IReadOnlyList<IArgumentOperation> arguments, FlowFrame frame)
    {
        switch (function)
        {
            case FlowValue.Function { Target: IAnonymousFunctionOperation lambda, Closure: var closure }:
                return InvokeLambda(lambda, closure, arguments, frame);
            case FlowValue.Function { Target: IMethodReferenceOperation { Method.MethodKind: MethodKind.LocalFunction } localFunction, Closure: var closure }:
                return Returns(localFunction.Method, Enclosed(closure, localFunction.Method, arguments, frame));
            case FlowValue.Function { Target: IMethodReferenceOperation reference, Closure: var closure }:
                IReadOnlyList<FlowValue?> receivers = reference.Instance is null ? [closure.This] : [.. Evaluate(reference.Instance, closure)];
                return [.. receivers.SelectMany(receiver => Returns(reference.Method, new FlowFrame
                {
                    Compilation = closure.Compilation,
                    This = receiver,
                    Arguments = _run.FindSource(reference.Method) is ({ } source, _) && source is IMethodSymbol sourceMethod ? Bind(sourceMethod, arguments, frame) : [],
                    Depth = closure.Depth,
                }))];
            case FlowValue.Gap gap:
                return [gap];
            default:
                return [new FlowValue.Gap(LocaleGapKind.Unsupported, "a delegate the flow cannot read")];
        }
    }

    private IReadOnlyList<FlowValue> InvokeLambda(IAnonymousFunctionOperation lambda, FlowFrame closure, IReadOnlyList<IArgumentOperation> arguments, FlowFrame frame)
    {
        var bound = new Dictionary<SymbolKey, (IOperation, FlowFrame)>(closure.Arguments);
        foreach (var argument in arguments)
        {
            if (argument.Parameter is { } parameter && parameter.Ordinal < lambda.Symbol.Parameters.Length)
            {
                bound[SymbolKey.Of(lambda.Symbol.Parameters[parameter.Ordinal])] = (argument.Value, frame);
            }
        }

        var entered = new FlowFrame { Compilation = closure.Compilation, This = closure.This, Arguments = bound, TypeArguments = closure.TypeArguments, Depth = closure.Depth + 1 };
        var returns = ReturnedValues(lambda.Body).ToArray();
        var values = returns.SelectMany(returned => Evaluate(returned, entered)).ToArray();

        return returns.Length > 1 ? Approximate(values) : values;
    }

    // A virtual call runs the override of the receiver's type, an interface call the implementation the receiver's type gives, and a
    // service the one implementation the run registers.
    private (IMethodSymbol Method, FlowValue? Receiver)? Dispatch(IMethodSymbol method, FlowValue? receiver, bool nonVirtual = false)
    {
        if (receiver is null || nonVirtual)
        {
            return (method, receiver);
        }

        if (receiver is FlowValue.Service service)
        {
            return _run.TryGetImplementation(service.Interface, out var implementation)
                ? Dispatch(method, new FlowValue.Allocated(implementation, null, null))
                : null;
        }

        var receiverType = receiver switch
        {
            FlowValue.Instance instance => instance.Type,
            FlowValue.Allocated allocated => allocated.Type,
            _ => null,
        };
        if (receiverType is null)
        {
            return null;
        }

        if (method.ContainingType.TypeKind == TypeKind.Interface)
        {
            var implementation = MostDerived(receiverType, method);

            return ReferenceEquals(implementation, method) ? null : (implementation, receiver);
        }

        return method.IsVirtual || method.IsOverride || method.IsAbstract ? (MostDerived(receiverType, method), receiver) : (method, receiver);
    }

    private static IEnumerable<ISymbol> AllMembers(INamedTypeSymbol type, string name)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers(name))
            {
                yield return member;
            }
        }
    }

    // The most derived override of a member the receiver's type runs, matched across compilations by declaration id.
    private static T MostDerived<T>(INamedTypeSymbol receiverType, T member)
        where T : class, ISymbol
    {
        var key = SymbolKey.Of(member);

        // An interface's member runs as the receiver's type implements it, matched by key, since the two may come from different compilations.
        if (member.ContainingType?.TypeKind == TypeKind.Interface)
        {
            foreach (var implemented in receiverType.AllInterfaces.SelectMany(face => face.GetMembers(member.Name)).OfType<T>())
            {
                if (SymbolKey.Of(implemented).Equals(key) && receiverType.FindImplementationForInterfaceMember(implemented) is T implementation)
                {
                    return implementation;
                }
            }

            return member;
        }

        foreach (var candidate in AllMembers(receiverType, member.Name).OfType<T>())
        {
            for (ISymbol? overridden = candidate; overridden is not null; overridden = Overridden(overridden))
            {
                if (SymbolKey.Of(overridden).Equals(key))
                {
                    return candidate;
                }
            }
        }

        return member;
    }

    private static ISymbol? Overridden(ISymbol symbol)
        => symbol switch
        {
            IMethodSymbol method => method.OverriddenMethod,
            IPropertySymbol property => property.OverriddenProperty,
            _ => null,
        };

    private static IPropertySymbol? FindProperty(INamedTypeSymbol type, string name)
        => AllMembers(type, name).OfType<IPropertySymbol>().FirstOrDefault(property => property.Parameters.IsEmpty);

    // A type overriding the member that holds the code, or one between it and the declaring type, runs its own member instead.
    private static bool OverridesBetween(INamedTypeSymbol derived, INamedTypeSymbol declaring, IMethodSymbol method)
    {
        var declaringKey = SymbolKey.Of(declaring);
        var methodKey = SymbolKey.Of(method);
        for (var type = derived; type is not null && !SymbolKey.Of(type).Equals(declaringKey); type = type.BaseType)
        {
            foreach (var candidate in type.GetMembers(method.Name).OfType<IMethodSymbol>())
            {
                for (var overridden = candidate.OverriddenMethod; overridden is not null; overridden = overridden.OverriddenMethod)
                {
                    if (SymbolKey.Of(overridden).Equals(methodKey))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static Dictionary<SymbolKey, (IOperation, FlowFrame)> Bind(IMethodSymbol method, IReadOnlyList<IArgumentOperation> arguments, FlowFrame callerFrame)
    {
        var bound = new Dictionary<SymbolKey, (IOperation, FlowFrame)>();
        foreach (var argument in arguments)
        {
            if (argument.Parameter is { Ordinal: var ordinal } && ordinal < method.Parameters.Length)
            {
                bound[SymbolKey.Of(method.Parameters[ordinal])] = (argument.Value, callerFrame);
            }
        }

        return bound;
    }

    private static Dictionary<SymbolKey, ITypeSymbol> BindTypes(IMethodSymbol dispatched, IMethodSymbol called, FlowFrame frame)
    {
        var bound = new Dictionary<SymbolKey, ITypeSymbol>();
        for (var index = 0; index < called.TypeArguments.Length && index < dispatched.OriginalDefinition.TypeParameters.Length; index++)
        {
            var argument = called.TypeArguments[index];
            bound[SymbolKey.Of(dispatched.OriginalDefinition.TypeParameters[index])] = argument is ITypeParameterSymbol parameter && frame.TypeArguments.TryGetValue(SymbolKey.Of(parameter), out var outer) ? outer : argument;
        }

        return bound;
    }

    private static IOperation? ArgumentFor(IReadOnlyList<IArgumentOperation> arguments, int ordinal)
        => arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == ordinal)?.Value;

    private IReadOnlyList<FlowValue> Injected(ITypeSymbol? type, ISymbol where)
    {
        if (type is null)
        {
            return [new FlowValue.Gap(LocaleGapKind.Reflection, where.Name)];
        }

        if (Generic(type) is { } generic)
        {
            return generic is INamedTypeSymbol { TypeKind: not TypeKind.Error } named
                ? [new FlowValue.Locale(named.OriginalDefinition, $"IStringLocale<{named.Name}>")]
                : [new FlowValue.Gap(LocaleGapKind.Unsupported, $"IStringLocale<{generic.Name}>")];
        }

        if (type.ToDisplayString() == LocaleFactoryMetadataName)
        {
            return [new FlowValue.Factory(true, $"the injected {where.Name}")];
        }

        if (type is INamedTypeSymbol { TypeKind: TypeKind.Interface } service)
        {
            return [new FlowValue.Service(service.OriginalDefinition)];
        }

        if (type is INamedTypeSymbol { TypeKind: TypeKind.Class } requested)
        {
            if (_run.TryGetImplementation(requested.OriginalDefinition, out var implementation))
            {
                return [new FlowValue.Allocated(implementation, null, null)];
            }

            return requested.IsSealed
                ? [new FlowValue.Allocated(requested.OriginalDefinition, null, null)]
                : [new FlowValue.Gap(LocaleGapKind.Dispatch, requested.Name)];
        }

        return [new FlowValue.Gap(LocaleGapKind.Reflection, where.Name)];
    }

    private static ITypeSymbol? Requested(IInvocationOperation invocation)
        => invocation.TargetMethod.TypeArguments is [var typeArgument]
            ? typeArgument
            : invocation.Arguments.Select(argument => argument.Value).OfType<ITypeOfOperation>().FirstOrDefault()?.TypeOperand;

    private static FlowValue Pass(FlowValue receiver, ISymbol member)
        => receiver as FlowValue.Gap ?? new FlowValue.Gap(LocaleGapKind.Unsupported, $"{member.Name} on a value the flow cannot read");

    private static IReadOnlyList<FlowValue> Locale(IReadOnlyList<FlowValue> types)
        => [.. types.Select(value => value switch
        {
            FlowValue.Named named => new FlowValue.Locale(named.Type, named.Root) { IsApproximate = named.IsApproximate },
            FlowValue.Gap gap => gap,
            _ => (FlowValue)new FlowValue.Gap(LocaleGapKind.Unsupported, "a type the flow cannot read"),
        })];

    private static ITypeSymbol? Generic(ITypeSymbol type)
        => type is INamedTypeSymbol { IsGenericType: true, TypeArguments: [var argument] } named && IsType(named, "Forma.Framework.Localization", "IStringLocale`1")
            ? argument
            : null;

    private static bool IsType(ITypeSymbol type, string namespaceName, string metadataName)
        => type.OriginalDefinition.MetadataName == metadataName && type.ContainingNamespace?.ToDisplayString() == namespaceName;

    private static bool Implements(ITypeSymbol? type, string metadataName)
        => type is not null
            && (type.OriginalDefinition.ToDisplayString() == metadataName
                || type.AllInterfaces.Any(candidate => candidate.OriginalDefinition.ToDisplayString() == metadataName));

    private static bool HasAttribute(ISymbol symbol, string attributeName)
        => symbol.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == attributeName);

    private static bool IsReachableFromOutside(IMethodSymbol method)
        => method.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal
            && method.ContainingType.DeclaredAccessibility == Accessibility.Public;

    private ISymbol? Enclosing(IOperation operation, Compilation compilation)
    {
        var symbol = _run.ModelOf(compilation, operation.Syntax.SyntaxTree).GetEnclosingSymbol(operation.Syntax.SpanStart);
        while (symbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction })
        {
            symbol = symbol.ContainingSymbol;
        }

        return symbol;
    }

    private IOperation? Body(IMethodSymbol method, Compilation compilation)
    {
        foreach (var declaration in method.DeclaringSyntaxReferences)
        {
            var node = declaration.GetSyntax();
            var model = _run.ModelOf(compilation, node.SyntaxTree);
            if ((model.GetOperation(node) ?? (node.Parent is { } parent ? model.GetOperation(parent) : null)) is { } body)
            {
                return body;
            }
        }

        return null;
    }

    // The values a body returns, its own returns alone: a nested lambda's or local function's are theirs, and Visual Basic's implicit
    // return of the function's own local is not a value written.
    private static IEnumerable<IOperation> ReturnedValues(IOperation body)
    {
        var pending = new Stack<IOperation>();
        pending.Push(body);
        while (pending.TryPop(out var current))
        {
            if (current is IReturnOperation returned)
            {
                if (returned.ReturnedValue is { } value && !(value.IsImplicit && value is ILocalReferenceOperation))
                {
                    yield return value;
                }

                continue;
            }

            foreach (var child in current.ChildOperations)
            {
                if (child is not (IAnonymousFunctionOperation or ILocalFunctionOperation))
                {
                    pending.Push(child);
                }
            }
        }
    }

    private static string Describe(IOperation operation)
        => operation.Syntax.ToString() is { Length: <= 60 } text ? text : operation.Kind.ToString();

    private static string Name(ISymbol symbol)
        => symbol is IMethodSymbol { AssociatedSymbol: { } associated }
            ? $"{associated.ContainingType?.Name}.{associated.Name}"
            : $"{symbol.ContainingType?.Name}.{symbol.Name}";
}
