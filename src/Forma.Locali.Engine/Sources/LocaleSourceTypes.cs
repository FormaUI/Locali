namespace Forma.Locali.Sources;

using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

/// <summary>Names the types a lookup through a member carrying <c>[LocaleSource]</c> reads, from the property that attribute names.</summary>
public static class LocaleSourceTypes
{
    // A table runs its factory outside its lock, so each entry is lazy and the trees of a project, walked at once, read it once.
    private static readonly ConditionalWeakTable<Compilation, Lazy<ILookup<INamedTypeSymbol, INamedTypeSymbol>>> DerivedByBase = [];

    /// <summary>The lookups with the types their unread properties name, read from the source of the projects of the run that build them.</summary>
    /// <param name="lookups">The lookups a project's callsites made.</param>
    /// <param name="compilations">The compilation of every project of the run.</param>
    public static IReadOnlyList<LocalizerLookup> ReadUnread(IReadOnlyList<LocalizerLookup> lookups, IReadOnlyList<Compilation> compilations)
    {
        ArgumentNullException.ThrowIfNull(lookups);
        ArgumentNullException.ThrowIfNull(compilations);

        // A project's lookups read the few properties its bases declare, so each is read once.
        var named = new Dictionary<IPropertySymbol, NamedType?>(SymbolEqualityComparer.Default);

        return
        [
            .. lookups.Select(lookup => lookup.UnreadProperties.Count == 0 ? lookup : lookup with
            {
                ResourceTypes = Distinct(lookup.ResourceTypes.Concat(lookup.UnreadProperties
                    .Select(unread => Name(GetOrRead(named, unread.Property, compilations), unread.Instance))
                    .OfType<INamedTypeSymbol>())),
                UnreadProperties = [],
            }),
        ];
    }

    /// <summary>The types a lookup through the receiver reads, one for each type of instance that reaches it, and the properties naming one that its compilation holds as metadata alone.</summary>
    /// <param name="receiver">The receiver the lookup was made through.</param>
    internal static (IReadOnlyList<INamedTypeSymbol> Types, IReadOnlyList<LocaleSourceProperty> Unread) Resolve(IOperation receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);

        // The attribute names a member of the same instance, so only a lookup through the instance its code runs on is read.
        if (Unconditional(receiver) is not IMemberReferenceOperation { Member: IPropertySymbol or IFieldSymbol, Instance: IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance } } reference
            || TypeMemberName(reference.Member) is not { } typeMemberName
            || FindProperty(reference.Member.ContainingType, typeMemberName) is not { } typeMember
            || reference.SemanticModel is not { } model
            || EnclosingMember(model, reference.Syntax.SpanStart) is not { ContainingType: { TypeKind: TypeKind.Class } declaring } enclosing)
        {
            return ([], []);
        }

        var types = new List<INamedTypeSymbol>();
        var unread = new List<LocaleSourceProperty>();
        foreach (var instance in Instances(model.Compilation, declaring, enclosing))
        {
            var property = MostDerived(instance, typeMember);
            if (property.GetMethod is not { } getter)
            {
                continue;
            }

            if (getter.ContainingAssembly is not ISourceAssemblySymbol { Compilation: var compilation } || getter.DeclaringSyntaxReferences.IsEmpty)
            {
                unread.Add(new LocaleSourceProperty(instance, property));
            }
            else if (Name(Read(getter, compilation), instance) is { } type)
            {
                types.Add(type);
            }
        }

        return (Distinct(types), unread);
    }

    // A conditional access binds its receiver once, outside the access it guards.
    private static IOperation? Unconditional(IOperation receiver)
    {
        if (receiver is not IConditionalAccessInstanceOperation)
        {
            return receiver;
        }

        var access = receiver.Parent;
        while (access is not null and not IConditionalAccessOperation)
        {
            access = access.Parent;
        }

        return (access as IConditionalAccessOperation)?.Operation;
    }

    private static string? TypeMemberName(ISymbol member)
        => member.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == LocalizerContract.SourceAttributeMetadataName)
            is { ConstructorArguments: [{ Value: string name }] }
            ? name
            : null;

    // A lambda or a local function runs as part of the member holding it.
    private static ISymbol? EnclosingMember(SemanticModel model, int position)
    {
        var symbol = model.GetEnclosingSymbol(position);
        while (symbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction })
        {
            symbol = symbol.ContainingSymbol;
        }

        return symbol;
    }

    // An instance of the declaring type runs the lookup, and so does one of a type derived from it that leaves the member holding it as it is.
    private static IEnumerable<INamedTypeSymbol> Instances(Compilation compilation, INamedTypeSymbol declaring, ISymbol enclosing)
    {
        if (!declaring.IsAbstract)
        {
            yield return declaring;
        }

        var derivedByBase = DerivedByBase.GetValue(compilation, key => new(() => ReadDerived(key))).Value;
        var pending = new Queue<INamedTypeSymbol>(derivedByBase[declaring.OriginalDefinition]);
        while (pending.TryDequeue(out var derived))
        {
            if (enclosing is IMethodSymbol method && Overrides(derived, declaring, method))
            {
                continue;
            }

            if (!derived.IsAbstract)
            {
                yield return derived;
            }

            foreach (var further in derivedByBase[derived])
            {
                pending.Enqueue(further);
            }
        }
    }

    private static ILookup<INamedTypeSymbol, INamedTypeSymbol> ReadDerived(Compilation compilation)
    {
        var classes = new List<INamedTypeSymbol>();
        Collect(compilation.Assembly.GlobalNamespace, classes);

        return classes
            .Where(type => type.BaseType is not null)
            .ToLookup<INamedTypeSymbol, INamedTypeSymbol>(type => type.BaseType!.OriginalDefinition, SymbolEqualityComparer.Default);
    }

    private static void Collect(INamespaceOrTypeSymbol container, List<INamedTypeSymbol> classes)
    {
        foreach (var type in container.GetTypeMembers())
        {
            if (type.TypeKind == TypeKind.Class)
            {
                classes.Add(type);
            }

            Collect(type, classes);
        }

        if (container is INamespaceSymbol space)
        {
            foreach (var child in space.GetNamespaceMembers())
            {
                Collect(child, classes);
            }
        }
    }

    // A type overriding the member that holds the lookup, or one between it and the declaring type, runs its own member instead.
    private static bool Overrides(INamedTypeSymbol derived, INamedTypeSymbol declaring, IMethodSymbol method)
    {
        for (var type = derived; type is not null && !SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, declaring.OriginalDefinition); type = type.BaseType)
        {
            if (type.GetMembers(method.Name).OfType<IMethodSymbol>().Any(candidate => Overrides(candidate.OverriddenMethod, method)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Overrides(IMethodSymbol? candidate, IMethodSymbol method)
    {
        for (; candidate is not null; candidate = candidate.OverriddenMethod)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, method.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private static IPropertySymbol? FindProperty(INamedTypeSymbol type, string name)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (current.GetMembers(name).OfType<IPropertySymbol>().FirstOrDefault(property => property.Parameters.IsEmpty) is { } property)
            {
                return property;
            }
        }

        return null;
    }

    // The property an instance reads is the most derived override of the one the attribute's type sees.
    private static IPropertySymbol MostDerived(INamedTypeSymbol instance, IPropertySymbol typeMember)
    {
        for (INamedTypeSymbol? type = instance; type is not null; type = type.BaseType)
        {
            if (type.GetMembers(typeMember.Name).OfType<IPropertySymbol>().FirstOrDefault(property => Overrides(property, typeMember)) is { } found)
            {
                return found;
            }
        }

        return typeMember;
    }

    private static bool Overrides(IPropertySymbol? candidate, IPropertySymbol property)
    {
        for (; candidate is not null; candidate = candidate.OverriddenProperty)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, property.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private static NamedType? GetOrRead(Dictionary<IPropertySymbol, NamedType?> named, IPropertySymbol property, IReadOnlyList<Compilation> compilations)
    {
        if (!named.TryGetValue(property, out var type))
        {
            named[property] = type = ReadFromRun(property, compilations);
        }

        return type;
    }

    // A getter a project holds as metadata is read in the source of the project of the run that builds its assembly.
    private static NamedType? ReadFromRun(IPropertySymbol property, IReadOnlyList<Compilation> compilations)
    {
        if (compilations.FirstOrDefault(compilation => compilation.AssemblyName == property.ContainingAssembly.Name) is not { } compilation
            || DocumentationCommentId.CreateDeclarationId(property.OriginalDefinition) is not { } id)
        {
            return null;
        }

        return DocumentationCommentId.GetSymbolsForDeclarationId(id, compilation)
            .OfType<IPropertySymbol>()
            .FirstOrDefault(candidate => SymbolEqualityComparer.Default.Equals(candidate.ContainingAssembly, compilation.Assembly))
            ?.GetMethod is { } getter
            ? Read(getter, compilation)
            : null;
    }

    // The getter's one return names the type: typeof names it, and the instance's GetType names the instance's own.
    private static NamedType? Read(IMethodSymbol getter, Compilation compilation)
    {
        foreach (var declaration in getter.DeclaringSyntaxReferences)
        {
            var node = declaration.GetSyntax();
            var model = compilation.GetSemanticModel(node.SyntaxTree);
            if ((model.GetOperation(node) ?? (node.Parent is { } parent ? model.GetOperation(parent) : null)) is not { } body)
            {
                continue;
            }

            // A return inside a lambda or a local function is that function's own.
            var operations = body.DescendantsAndSelf().ToArray();
            if (operations.Any(operation => operation is IAnonymousFunctionOperation or ILocalFunctionOperation)
                || operations.OfType<IReturnOperation>().Where(operation => operation.ReturnedValue is { IsImplicit: false }).ToArray() is not [{ ReturnedValue: { } returned }])
            {
                return null;
            }

            return Unwrapped(returned) switch
            {
                ITypeOfOperation { TypeOperand: INamedTypeSymbol { TypeKind: not TypeKind.Error } type } => new NamedType(type.OriginalDefinition),
                IInvocationOperation
                {
                    TargetMethod: { Name: nameof(GetType), Parameters.IsEmpty: true, ContainingType.SpecialType: SpecialType.System_Object },
                    Instance: IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance },
                } => new NamedType(null),
                _ => null,
            };
        }

        return null;
    }

    // The factory reads a type by its open definition, the instance's own included.
    private static INamedTypeSymbol? Name(NamedType? named, INamedTypeSymbol instance)
        => named is { } read ? read.Type ?? instance.OriginalDefinition : null;

    private static IReadOnlyList<INamedTypeSymbol> Distinct(IEnumerable<INamedTypeSymbol> types)
        => [.. types.Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)];

    private static IOperation Unwrapped(IOperation operation)
        => operation switch
        {
            IConversionOperation conversion => Unwrapped(conversion.Operand),
            IParenthesizedOperation parenthesized => Unwrapped(parenthesized.Operand),
            _ => operation,
        };

    // What a getter's return names: a type, or the instance's own where the type is null.
    private readonly record struct NamedType(INamedTypeSymbol? Type);
}
