namespace Forma.Locali.Flow;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

/// <summary>Finds every write to a field, a property or a local in source.</summary>
internal static class FlowWrites
{
    private const string BackingFieldKeyword = "field";

    /// <summary>Every write to a field or a property in its type's source, and a write from outside where its accessibility allows one.</summary>
    /// <param name="run">What the flow reads across the run.</param>
    /// <param name="member">The field or property, in source.</param>
    public static IReadOnlyList<FlowWrite> Of(FlowRun run, ISymbol member)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(member);

        var writes = new List<FlowWrite>();
        if (IsSettableOutside(member))
        {
            writes.Add(new FlowWrite(FlowWriteKind.Outside, null, null));
        }

        if (member.ContainingAssembly is not ISourceAssemblySymbol { Compilation: var compilation } || member.ContainingType is not { } type)
        {
            return writes;
        }

        var key = SymbolKey.Of(member);
        var names = Names(member);
        var comparison = compilation.Language == LanguageNames.VisualBasic ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var seen = new HashSet<IOperation>(ReferenceEqualityComparer.Instance);

        foreach (var part in type.DeclaringSyntaxReferences)
        {
            var root = part.GetSyntax();
            var model = run.ModelOf(compilation, root.SyntaxTree);
            foreach (var token in root.DescendantTokens())
            {
                if (!names.Any(name => string.Equals(token.ValueText, name, comparison)) || token.Parent is not { } node)
                {
                    continue;
                }

                if (model.GetDeclaredSymbol(node) is { } declared && SymbolKey.Of(declared).Equals(key))
                {
                    AddInitializers(model, node, key, writes, seen);
                    continue;
                }

                if (Reference(model, node, key) is { } reference && seen.Add(reference))
                {
                    AddWrite(model, reference, type, writes);
                }
            }
        }

        return writes;
    }

    /// <summary>The value of every write to a local in its member's body, more than one where it is assigned again, passed by reference, or declared without being assigned.</summary>
    /// <param name="local">The local.</param>
    /// <param name="body">The operation holding every use of the local.</param>
    public static IReadOnlyList<IOperation> OfLocal(ILocalSymbol local, IOperation body)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(body);

        var values = new List<IOperation>();
        if (local.IsRef)
        {
            // A ref local aliases another location, whose writes are not the local's own.
            return [body, body];
        }

        foreach (var operation in body.DescendantsAndSelf())
        {
            switch (operation)
            {
                case IVariableDeclaratorOperation declarator when SymbolEqualityComparer.Default.Equals(declarator.Symbol, local):
                    if (declarator.GetVariableInitializer()?.Value is { } initial)
                    {
                        values.Add(initial);
                    }

                    break;
                case ISimpleAssignmentOperation { Target: ILocalReferenceOperation target } assignment when SymbolEqualityComparer.Default.Equals(target.Local, local):
                    values.Add(assignment.Value);
                    break;
                case ICompoundAssignmentOperation { Target: ILocalReferenceOperation target } when SymbolEqualityComparer.Default.Equals(target.Local, local):
                case ICoalesceAssignmentOperation { Target: ILocalReferenceOperation coalesced } when SymbolEqualityComparer.Default.Equals(coalesced.Local, local):
                case IArgumentOperation { Value: ILocalReferenceOperation passed, Parameter.RefKind: RefKind.Ref or RefKind.Out } when SymbolEqualityComparer.Default.Equals(passed.Local, local):
                case IDeclarationPatternOperation { DeclaredSymbol: ILocalSymbol declared } when SymbolEqualityComparer.Default.Equals(declared, local):
                case IForEachLoopOperation loop when loop.Locals.Contains(local, SymbolEqualityComparer.Default):
                case IDeconstructionAssignmentOperation deconstruction when deconstruction.Target.DescendantsAndSelf().OfType<ILocalReferenceOperation>().Any(target => SymbolEqualityComparer.Default.Equals(target.Local, local)):
                    // A second entry stands for every write the flow cannot pair with one value.
                    values.Add(operation);
                    values.Add(operation);
                    break;
            }
        }

        return values;
    }

    // A field kept private or read-only, or a property whose setter is private or absent, is written by its own type alone.
    private static bool IsSettableOutside(ISymbol member)
        => member switch
        {
            IFieldSymbol field => !field.IsReadOnly && !field.IsConst && field.AssociatedSymbol is null && field.DeclaredAccessibility != Accessibility.Private,
            IPropertySymbol property => property.SetMethod is { DeclaredAccessibility: not Accessibility.Private },
            _ => false,
        };

    // A property backed by `field` is written through the keyword in its own accessors as well as through its name.
    private static string[] Names(ISymbol member)
        => member is IFieldSymbol { AssociatedSymbol: IPropertySymbol property }
            ? [member.Name, property.Name, BackingFieldKeyword]
            : [member.Name];

    private static IOperation? Reference(SemanticModel model, SyntaxNode node, SymbolKey key)
    {
        for (var (candidate, level) = (node, 0); candidate is not null && level < 3; candidate = candidate.Parent, level++)
        {
            switch (model.GetOperation(candidate))
            {
                case IFieldReferenceOperation field when SymbolKey.Of(field.Field).Equals(key):
                    return field;
                case IPropertyReferenceOperation property when SymbolKey.Of(property.Property).Equals(key):
                    return property;
            }
        }

        return null;
    }

    private static void AddInitializers(SemanticModel model, SyntaxNode declaration, SymbolKey key, List<FlowWrite> writes, HashSet<IOperation> seen)
    {
        foreach (var candidate in (declaration.Parent ?? declaration).DescendantNodesAndSelf())
        {
            var initialized = model.GetOperation(candidate) switch
            {
                IFieldInitializerOperation field when field.InitializedFields.Any(initializedField => SymbolKey.Of(initializedField).Equals(key)) => field,
                IPropertyInitializerOperation property when property.InitializedProperties.Any(initializedProperty => SymbolKey.Of(initializedProperty).Equals(key)) => (ISymbolInitializerOperation)property,
                _ => null,
            };

            if (initialized is not null && seen.Add(initialized))
            {
                writes.Add(new FlowWrite(FlowWriteKind.Initializer, initialized.Value, null));
            }
        }
    }

    private static void AddWrite(SemanticModel model, IOperation reference, INamedTypeSymbol type, List<FlowWrite> writes)
    {
        var owner = Owner(model, reference);
        if (reference is IMemberReferenceOperation { Instance: { } instance } && instance is not IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance }
            && reference.Parent is ISimpleAssignmentOperation or ICoalesceAssignmentOperation)
        {
            // A write to another object, or to one an initializer is building, pairs with no one object the lookup reads.
            writes.Add(new FlowWrite(FlowWriteKind.Other, null, owner));
            return;
        }

        switch (reference.Parent)
        {
            case ISimpleAssignmentOperation assignment when assignment.Target == reference:
                var isConstructor = owner is { MethodKind: MethodKind.Constructor } constructor && SymbolKey.Of(constructor.ContainingType).Equals(SymbolKey.Of(type));
                writes.Add(new FlowWrite(isConstructor ? FlowWriteKind.Constructor : FlowWriteKind.Other, assignment.Value, owner));
                break;
            case ICoalesceAssignmentOperation coalesce when coalesce.Target == reference:
                writes.Add(new FlowWrite(FlowWriteKind.LazyInitializer, coalesce.Value, owner));
                break;
            case ICompoundAssignmentOperation compound when compound.Target == reference:
            case IIncrementOrDecrementOperation:
            case IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out }:
            case ITupleOperation tuple when IsDeconstructed(tuple):
            case IVariableInitializerOperation { Parent: IVariableDeclaratorOperation { Symbol.IsRef: true } }:
                // A deconstruction, or a location taken by reference, writes the member where the flow cannot pair the value.
                writes.Add(new FlowWrite(FlowWriteKind.Other, null, owner));
                break;
        }
    }

    // A tuple is written where it, or a tuple holding it, is a deconstruction's target.
    private static bool IsDeconstructed(ITupleOperation tuple)
    {
        IOperation current = tuple;
        while (current.Parent is ITupleOperation outer)
        {
            current = outer;
        }

        return current.Parent is IDeconstructionAssignmentOperation deconstruction && deconstruction.Target == current;
    }

    // A write in a lambda or a local function belongs to the member holding it.
    private static IMethodSymbol? Owner(SemanticModel model, IOperation operation)
    {
        var symbol = model.GetEnclosingSymbol(operation.Syntax.SpanStart);
        while (symbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction })
        {
            symbol = symbol.ContainingSymbol;
        }

        return symbol as IMethodSymbol;
    }
}
