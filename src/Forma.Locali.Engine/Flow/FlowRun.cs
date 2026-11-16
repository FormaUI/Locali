namespace Forma.Locali.Flow;

using System.Collections.Concurrent;

using Forma.Locali.Configuration;
using Forma.Locali.Projects;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

using CSharp = Microsoft.CodeAnalysis.CSharp.Syntax;
using VisualBasic = Microsoft.CodeAnalysis.VisualBasic.Syntax;

/// <summary>What the flow reads across a run: its compilations, which of them are tests, the calls and creations each holds, and the services the run registers.</summary>
internal sealed class FlowRun
{
    private const string DependencyInjectionNamespace = "Microsoft.Extensions.DependencyInjection";
    private const string ServiceDescriptorName = "ServiceDescriptor";

    private readonly Dictionary<Compilation, LoadedProject> _projects = [with(ReferenceEqualityComparer.Instance)];
    private readonly ConcurrentDictionary<(Compilation Compilation, string Name), IReadOnlyList<IOperation>> _callsByName = new();
    private readonly ConcurrentDictionary<Compilation, Lazy<ILookup<SymbolKey, IOperation>>> _creations = new(ReferenceEqualityComparer.Instance);
    private readonly ConcurrentDictionary<Compilation, Lazy<ILookup<SymbolKey, INamedTypeSymbol>>> _derived = new(ReferenceEqualityComparer.Instance);
    private readonly ConcurrentDictionary<SyntaxTree, SemanticModel> _models = new();
    private const string ServiceCollectionName = "IServiceCollection";
    private const string AdapterNamespace = "Forma.Framework.Localization.StringLocalizerAdapter";
    private const string AdapterRegistration = "AddFormaStringLocalizerAdapter";
    private const string LocalizationSuffix = "Localization";

    private static readonly string[] RegistrationNames = ["Add", "TryAdd", "Replace", "Decorate", "Scoped", "Singleton", "Transient", "Describe"];

    private readonly ConcurrentDictionary<SymbolKey, Lazy<IReadOnlyList<INamedTypeSymbol?>>> _registrations = new();
    private readonly Lazy<bool> _isStringLocalizerAdapted;

    /// <summary>Reads the run's projects.</summary>
    /// <param name="projects">Every project the run loaded.</param>
    /// <param name="hints">Every member hint the run's configuration names.</param>
    public FlowRun(IReadOnlyList<LoadedProject> projects, IReadOnlyList<LocaleMemberHint> hints)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(hints);

        foreach (var project in projects.OrderBy(project => project.Path, StringComparer.Ordinal))
        {
            if (project.Compilation is { } compilation)
            {
                _projects.TryAdd(compilation, project);
            }
        }

        Compilations = [.. _projects.Keys];
        Hints = new LocaleMemberHints(Compilations, hints);
        _isStringLocalizerAdapted = new(ReadStringLocalizerAdapted);
    }

    /// <summary>The compilation of every project of the run, ordered by the project's path.</summary>
    public IReadOnlyList<Compilation> Compilations { get; }

    /// <summary>The member hints the run applies.</summary>
    public LocaleMemberHints Hints { get; }

    /// <summary>Whether Forma's adapter answers Microsoft's string localizer across the run: its product code registers the adapter, ahead of any of Microsoft's localization, and registers neither contract in the adapter's place.</summary>
    public bool IsStringLocalizerAdapted
        => _isStringLocalizerAdapted.Value;

    /// <summary>The semantic model of a tree, made once and shared, so a member bound for one lookup is bound for every other.</summary>
    /// <param name="compilation">The compilation holding the tree.</param>
    /// <param name="tree">The tree.</param>
    public SemanticModel ModelOf(Compilation compilation, SyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(tree);

        return _models.GetOrAdd(tree, static (key, owner) => owner.GetSemanticModel(key), compilation);
    }

    /// <summary>The project a compilation belongs to, or null when it is none of the run's.</summary>
    /// <param name="compilation">The compilation.</param>
    public LoadedProject? ProjectOf(Compilation compilation)
        => _projects.GetValueOrDefault(compilation);

    /// <summary>Whether a compilation is a test project's, whose calls and registrations never speak for product code.</summary>
    /// <param name="compilation">The compilation.</param>
    public bool IsTest(Compilation compilation)
        => ProjectOf(compilation)?.IsTestProject == true;

    /// <summary>A symbol's declaration in source and the compilation holding it: its own where it is in source, or that of the project of the run building its assembly; null where no project of the run does.</summary>
    /// <param name="symbol">The symbol, from any compilation of the run.</param>
    public (ISymbol Symbol, Compilation Compilation)? FindSource(ISymbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        var definition = symbol.OriginalDefinition;
        if (!definition.DeclaringSyntaxReferences.IsEmpty && definition.ContainingAssembly is ISourceAssemblySymbol { Compilation: var own } && _projects.ContainsKey(own))
        {
            return (definition, own);
        }

        if (DocumentationCommentId.CreateDeclarationId(definition) is not { } id)
        {
            return null;
        }

        foreach (var compilation in Compilations)
        {
            if (compilation.AssemblyName != definition.ContainingAssembly?.Name)
            {
                continue;
            }

            var found = DocumentationCommentId.GetSymbolsForDeclarationId(id, compilation)
                .FirstOrDefault(candidate => SymbolEqualityComparer.Default.Equals(candidate.ContainingAssembly, compilation.Assembly) && !candidate.DeclaringSyntaxReferences.IsEmpty);
            if (found is not null)
            {
                return (found, compilation);
            }
        }

        return null;
    }

    /// <summary>Every call to a method, and every creation or chained call of a constructor, in one compilation.</summary>
    /// <param name="method">The method or constructor.</param>
    /// <param name="compilation">The compilation to search.</param>
    public IEnumerable<IOperation> CallsOf(IMethodSymbol method, Compilation compilation)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(compilation);

        var key = SymbolKey.Of(method);
        if (method.MethodKind == MethodKind.Constructor)
        {
            return _creations.GetOrAdd(compilation, static (owner, run) => new(() => run.ReadCreations(owner)), this).Value[key];
        }

        // A name narrows the search to the nodes that can call the method, and the symbol each binds to settles it.
        return _callsByName.GetOrAdd((compilation, method.Name), static (entry, run) => run.ReadCalls(entry.Compilation, entry.Name), this)
            .Where(call => call is IInvocationOperation invocation && SymbolKey.Of(invocation.TargetMethod).Equals(key)
                || call is IMethodReferenceOperation reference && SymbolKey.Of(reference.Method).Equals(key));
    }

    /// <summary>Every class of a compilation deriving from a type, directly or through another.</summary>
    /// <param name="compilation">The compilation.</param>
    /// <param name="type">The base type.</param>
    public IEnumerable<INamedTypeSymbol> DerivedFrom(Compilation compilation, INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(type);

        var derivedByBase = _derived.GetOrAdd(compilation, static owner => new(() => ReadDerived(owner))).Value;
        var pending = new Queue<INamedTypeSymbol>(derivedByBase[SymbolKey.Of(type)]);
        while (pending.TryDequeue(out var derived))
        {
            yield return derived;

            foreach (var further in derivedByBase[SymbolKey.Of(derived)])
            {
                pending.Enqueue(further);
            }
        }
    }

    /// <summary>The one implementation the run's product code registers for a service, where the service is a type of a project of the run, every registration naming it can be read, and they agree.</summary>
    /// <param name="service">The service, an interface or a class.</param>
    /// <param name="implementation">The implementation, when there is one.</param>
    public bool TryGetImplementation(INamedTypeSymbol service, out INamedTypeSymbol implementation)
    {
        ArgumentNullException.ThrowIfNull(service);

        implementation = service;
        if (!Compilations.Any(compilation => compilation.AssemblyName == service.ContainingAssembly?.Name))
        {
            return false;
        }

        var registered = _registrations.GetOrAdd(SymbolKey.Of(service.OriginalDefinition), static (_, entry) => new(() => entry.Run.ReadRegistrations(entry.Service)), (Run: this, Service: service.OriginalDefinition)).Value;
        if (registered.Count == 0 || registered.Any(candidate => candidate is null))
        {
            return false;
        }

        if (registered.DistinctBy(candidate => SymbolKey.Of(candidate!)).ToArray() is not [var single])
        {
            return false;
        }

        implementation = single!;

        return true;
    }

    /// <summary>Whether a class of the run derives from a type.</summary>
    /// <param name="type">The type.</param>
    public bool HasDerived(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var key = SymbolKey.Of(type.OriginalDefinition);

        return Compilations.Any(compilation => _derived.GetOrAdd(compilation, static owner => new(() => ReadDerived(owner))).Value.Contains(key));
    }

    private static ILookup<SymbolKey, INamedTypeSymbol> ReadDerived(Compilation compilation)
    {
        var classes = new List<INamedTypeSymbol>();
        Collect(compilation.Assembly.GlobalNamespace, classes);

        return classes.Where(type => type.BaseType is not null).ToLookup(type => SymbolKey.Of(type.BaseType!));
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

    // A call is found by its name's token, and the first call or method reference above it is settled by the symbol it binds to.
    private IReadOnlyList<IOperation> ReadCalls(Compilation compilation, string name)
    {
        var comparison = compilation.Language == LanguageNames.VisualBasic ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var calls = new HashSet<IOperation>(ReferenceEqualityComparer.Instance);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = ModelOf(compilation, tree);
            foreach (var token in tree.GetRoot().DescendantTokens())
            {
                if (string.Equals(token.ValueText, name, comparison) && CallAbove(model, token) is { } call)
                {
                    calls.Add(call);
                }
            }
        }

        return [.. calls];
    }

    // A name sits a few nodes below the call it makes: in a member access, a generic name or a conditional access.
    private static IOperation? CallAbove(SemanticModel model, SyntaxToken token)
    {
        var node = token.Parent;
        for (var level = 0; node is not null && level < 4; level++, node = node.Parent)
        {
            if (model.GetOperation(node) is IInvocationOperation or IMethodReferenceOperation)
            {
                return model.GetOperation(node);
            }
        }

        return null;
    }

    // Creations and chained constructor calls have no name to search for when written as `new(...)`, `base(...)` or `MyBase.New(...)`.
    private ILookup<SymbolKey, IOperation> ReadCreations(Compilation compilation)
    {
        var creations = new List<(SymbolKey Key, IOperation Operation)>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = ModelOf(compilation, tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                if (node is not (CSharp.BaseObjectCreationExpressionSyntax or CSharp.ConstructorInitializerSyntax or CSharp.PrimaryConstructorBaseTypeSyntax
                    or VisualBasic.ObjectCreationExpressionSyntax or VisualBasic.InvocationExpressionSyntax))
                {
                    continue;
                }

                switch (model.GetOperation(node))
                {
                    case IObjectCreationOperation { Constructor: { } constructor } creation:
                        creations.Add((SymbolKey.Of(constructor), creation));
                        break;
                    case IInvocationOperation { TargetMethod.MethodKind: MethodKind.Constructor } chained:
                        creations.Add((SymbolKey.Of(chained.TargetMethod), chained));
                        break;
                }
            }
        }

        return creations.ToLookup(entry => entry.Key, entry => entry.Operation);
    }

    // A registration names its service where it is written: as a type argument or a `typeof` of one of the container's own
    // registrations, or of a descriptor. Each call written around the service's name is read: a registration or a descriptor names
    // what implements it, or leaves that unknown where it passes an instance or a factory the flow cannot type, and any other call
    // handed the service collection may register it as anything. Test projects register fakes, so only product code is read.
    private List<INamedTypeSymbol?> ReadRegistrations(INamedTypeSymbol service)
    {
        var key = SymbolKey.Of(service);
        var registrations = new List<INamedTypeSymbol?>();

        foreach (var compilation in Compilations.Where(compilation => !IsTest(compilation)))
        {
            var comparison = compilation.Language == LanguageNames.VisualBasic ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            foreach (var tree in compilation.SyntaxTrees)
            {
                var read = new HashSet<SyntaxNode>();
                foreach (var token in tree.GetRoot().DescendantTokens())
                {
                    if (!string.Equals(token.ValueText, service.Name, comparison)
                        || RegistrationAbove(ModelOf(compilation, tree), token) is not { } call
                        || !read.Add(call.Syntax))
                    {
                        continue;
                    }

                    switch (call)
                    {
                        case IObjectCreationOperation descriptor:
                            var typeOfs = descriptor.Arguments.Select(argument => Unwrap(argument.Value)).OfType<ITypeOfOperation>().ToArray();
                            if (typeOfs is [{ TypeOperand: INamedTypeSymbol described }, ..] && SymbolKey.Of(described.OriginalDefinition).Equals(key))
                            {
                                registrations.Add(typeOfs is [_, { TypeOperand: INamedTypeSymbol implementation }] ? implementation.OriginalDefinition : null);
                            }

                            break;
                        case IInvocationOperation invocation when IsContainerRegistration(invocation, comparison):
                            if (Service(invocation) is INamedTypeSymbol registered && SymbolKey.Of(registered).Equals(key))
                            {
                                var isKeyed = invocation.TargetMethod.Name.Contains("Keyed", StringComparison.Ordinal);
                                registrations.Add(isKeyed ? null : Implementation(invocation, registered));
                            }

                            break;
                        case IInvocationOperation invocation when TouchesServiceCollection(invocation):
                            registrations.Add(null);
                            break;
                    }
                }
            }
        }

        return registrations;
    }

    // The adapter and Microsoft's localization, AddLocalization and AddViewLocalization among its calls, each register the factory only
    // where nothing is registered yet, so a Microsoft call answers in the adapter's place unless the adapter registered first, before it
    // in the same block; and the application registering either contract itself answers in place of both.
    private bool ReadStringLocalizerAdapted()
    {
        var adapters = new List<IOperation>();
        var microsofts = new List<IOperation>();
        foreach (var compilation in Compilations.Where(compilation => !IsTest(compilation)))
        {
            var comparison = compilation.Language == LanguageNames.VisualBasic ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            foreach (var tree in compilation.SyntaxTrees)
            {
                foreach (var token in tree.GetRoot().DescendantTokens())
                {
                    if (!string.Equals(token.ValueText, AdapterRegistration, comparison) && !token.ValueText.EndsWith(LocalizationSuffix, comparison)
                        || CallAbove(ModelOf(compilation, tree), token) is not IInvocationOperation { TargetMethod: var method } invocation)
                    {
                        continue;
                    }

                    var containingNamespace = method.ContainingNamespace?.ToDisplayString();
                    if (method.Name == AdapterRegistration && containingNamespace == AdapterNamespace)
                    {
                        adapters.Add(invocation);
                    }
                    else if (method.Name.EndsWith(LocalizationSuffix, StringComparison.Ordinal) && containingNamespace?.StartsWith("Microsoft.", StringComparison.Ordinal) == true)
                    {
                        microsofts.Add(invocation);
                    }
                }
            }
        }

        return adapters.Count > 0
            && microsofts.All(microsoft => adapters.Any(adapter => RunsBefore(adapter, microsoft)))
            && Answers("Microsoft.Extensions.Localization.IStringLocalizerFactory", implementation => implementation.ContainingAssembly?.Name == AdapterNamespace)
            && Answers("Microsoft.Extensions.Localization.IStringLocalizer`1", implementation => implementation.MetadataName == "StringLocalizer`1" && implementation.ContainingNamespace?.ToDisplayString() == "Microsoft.Extensions.Localization");
    }

    // Whether one call's statement comes before another's in the same block, which runs them in that order.
    private static bool RunsBefore(IOperation earlier, IOperation later)
        => Statement(earlier) is { Parent: IBlockOperation block } first
            && Statement(later) is { Parent: IBlockOperation laterBlock } second
            && block.Syntax == laterBlock.Syntax
            && first.Syntax.SpanStart < second.Syntax.SpanStart;

    private static IOperation? Statement(IOperation operation)
    {
        for (var current = operation; current.Parent is { } parent; current = parent)
        {
            if (parent is IBlockOperation)
            {
                return current;
            }
        }

        return null;
    }

    // Whether every registration the run's product code writes for a contract is one the adapter makes.
    private bool Answers(string contractMetadataName, Func<INamedTypeSymbol, bool> isAdapters)
        => Compilations.Select(compilation => compilation.GetTypeByMetadataName(contractMetadataName)).FirstOrDefault(found => found is not null) is not { } contract
            || ReadRegistrations(contract).All(implementation => implementation is not null && isAdapters(implementation));

    // The call the service's name is written in: the nearest creation or invocation above it.
    private static IOperation? RegistrationAbove(SemanticModel model, SyntaxToken token)
    {
        for (var (node, level) = (token.Parent, 0); node is not null && level < 6; node = node.Parent, level++)
        {
            switch (model.GetOperation(node))
            {
                case IObjectCreationOperation { Type: INamedTypeSymbol type } creation:
                    return IsServiceDescriptor(type) ? creation : null;
                case IInvocationOperation invocation:
                    return invocation;
            }
        }

        return null;
    }

    // The container's own registrations: its collection's extensions, and the descriptors it builds.
    private static bool IsContainerRegistration(IInvocationOperation invocation, StringComparison comparison)
        => invocation.TargetMethod.ContainingNamespace?.ToDisplayString().StartsWith(DependencyInjectionNamespace, StringComparison.Ordinal) == true
            && RegistrationNames.Any(prefix => invocation.TargetMethod.Name.StartsWith(prefix, comparison));

    private static bool TouchesServiceCollection(IInvocationOperation invocation)
        => IsServiceCollection(invocation.Instance?.Type) || invocation.Arguments.Any(argument => IsServiceCollection(argument.Value.Type));

    private static bool IsServiceCollection(ITypeSymbol? type)
        => type is not null
            && (IsCollectionInterface(type) || type.AllInterfaces.Any(IsCollectionInterface));

    private static bool IsCollectionInterface(ITypeSymbol type)
        => type.Name == ServiceCollectionName && type.ContainingNamespace?.ToDisplayString() == DependencyInjectionNamespace;

    private static bool IsServiceDescriptor(INamedTypeSymbol type)
        => type.Name == ServiceDescriptorName && type.ContainingNamespace?.ToDisplayString() == DependencyInjectionNamespace;

    // The service a registration names, by its first type argument or its first `typeof`.
    private static ITypeSymbol? Service(IInvocationOperation invocation)
        => invocation.TargetMethod.TypeArguments is [var service, ..]
            ? service is INamedTypeSymbol named ? named.OriginalDefinition : service
            : invocation.Arguments.Select(argument => Unwrap(argument.Value)).OfType<ITypeOfOperation>().FirstOrDefault()?.TypeOperand switch
            {
                INamedTypeSymbol typed => typed.OriginalDefinition,
                var other => other,
            };

    private static INamedTypeSymbol? Implementation(IInvocationOperation invocation, INamedTypeSymbol service)
    {
        var typeArguments = invocation.TargetMethod.TypeArguments;
        if (typeArguments.Length == 2)
        {
            return typeArguments[1] is INamedTypeSymbol implementation ? implementation.OriginalDefinition : null;
        }

        var typeOfs = invocation.Arguments.Select(argument => Unwrap(argument.Value)).OfType<ITypeOfOperation>().ToArray();
        if (typeOfs.Length == 2)
        {
            return typeOfs[1].TypeOperand is INamedTypeSymbol typedImplementation ? typedImplementation.OriginalDefinition : null;
        }

        // Beside the collection an extension method is called on, a registration of one service passes nothing, a factory, or an instance.
        var passed = invocation.Arguments
            .Where(argument => !(invocation.TargetMethod.IsExtensionMethod && argument.Parameter?.Ordinal == 0))
            .Select(argument => Unwrap(argument.Value))
            .Where(value => value is not ITypeOfOperation)
            .ToArray();

        return passed switch
        {
            [] => service,
            [IAnonymousFunctionOperation factory] => Created(factory),
            [IObjectCreationOperation { Type: INamedTypeSymbol created }] => created.OriginalDefinition,
            _ => null,
        };
    }

    // A factory names its implementation where every value it returns is a creation of one type.
    private static INamedTypeSymbol? Created(IAnonymousFunctionOperation factory)
    {
        var created = factory.Body.Descendants()
            .OfType<IReturnOperation>()
            .Select(returned => returned.ReturnedValue is { } value ? Unwrap(value) : null)
            .ToArray();

        if (created.Length == 0 || created.Any(value => value is not IObjectCreationOperation { Type: INamedTypeSymbol }))
        {
            return null;
        }

        var types = created.Select(value => ((INamedTypeSymbol)value!.Type!).OriginalDefinition).DistinctBy(SymbolKey.Of).ToArray();

        return types is [var single] ? single : null;
    }

    private static IOperation Unwrap(IOperation operation)
        => operation switch
        {
            IConversionOperation conversion => Unwrap(conversion.Operand),
            IDelegateCreationOperation creation => Unwrap(creation.Target),
            IParenthesizedOperation parenthesized => Unwrap(parenthesized.Operand),
            _ => operation,
        };
}
