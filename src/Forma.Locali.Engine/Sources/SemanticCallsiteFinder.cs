namespace Forma.Locali.Sources;

using System.Collections.Immutable;

using Forma.Locali.Flow;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

using CSharp = Microsoft.CodeAnalysis.CSharp.Syntax;
using VisualBasic = Microsoft.CodeAnalysis.VisualBasic.Syntax;

/// <summary>Finds callsites by binding the receiver to the localizer contract, and text carried as data by binding a call to the runtime's <c>LocalizableText.For</c>.</summary>
public sealed class SemanticCallsiteFinder : ILocalizerCallsiteFinder
{
    /// <inheritdoc />
    public bool CanRead(Compilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        return compilation.GetTypeByMetadataName(LocalizerContract.MetadataName) is not null;
    }

    /// <inheritdoc />
    public LocalizerLookups Find(Compilation compilation, SyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(tree);

        var contract = compilation.GetTypeByMetadataName(LocalizerContract.MetadataName);

        if (contract is null)
        {
            return LocalizerLookups.Empty;
        }

        var generic = compilation.GetTypeByMetadataName(LocalizerContract.GenericMetadataName);
        var stringLocalizer = new StringLocalizerTypes(compilation.GetTypeByMetadataName(LocalizerContract.StringLocalizerMetadataName), compilation.GetTypeByMetadataName(LocalizerContract.StringLocalizerGenericMetadataName));

        return Walk(compilation, tree, contract, Candidates(compilation, tree), operation => Lookup(operation, contract, generic, stringLocalizer));
    }

    /// <inheritdoc />
    public LocalizerLookups FindKeyed(Compilation compilation, SyntaxTree tree, KeyedMethods keyed)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(keyed);

        if (keyed.IsEmpty || compilation.GetTypeByMetadataName(LocalizerContract.MetadataName) is not { } contract)
        {
            return LocalizerLookups.Empty;
        }

        // A hinted call's locale is followed from the call itself, and the values it passes beside its key are the method's to use.
        return Walk(
            compilation,
            tree,
            contract,
            KeyedCandidates(compilation, tree, keyed.Names),
            operation => operation is IInvocationOperation invocation && keyed.KeyOrdinal(invocation.TargetMethod) is { } ordinal
                ? new BoundLookup(invocation.Arguments, ordinal, ([], invocation.Syntax), KeyedMethod: invocation.TargetMethod, IsHinted: true)
                : null);
    }

    // The operation tree is what the compiler bound, in the same shape for every language it reads, so the
    // walk reads it rather than the syntax; an indexer and a default property are one operation there.
    private static BoundLookup? Lookup(IOperation operation, INamedTypeSymbol contract, INamedTypeSymbol? generic, StringLocalizerTypes stringLocalizer)
        => operation switch
        {
            IPropertyReferenceOperation { Arguments.Length: > 0 } indexer
                when stringLocalizer.Contract is { } localizer && LocalizerContract.Implements(indexer.Property.ContainingType, localizer)
                => new(indexer.Arguments, 0, (stringLocalizer.Generic is not null && LocalizerContract.ResourceOf(indexer.Instance?.Type, stringLocalizer.Generic) is { } resource ? [new LocaleOrigin(resource, $"IStringLocalizer<{resource.Name}>")] : [], null), KeyedMethod: null, UsesStringLocalizer: true),
            IPropertyReferenceOperation { Arguments.Length: > 0 } indexer
                when LocalizerContract.Implements(indexer.Property.ContainingType, contract)
                => new(indexer.Arguments, 0, Resources(indexer, indexer.Instance, generic), KeyedMethod: null),
            IInvocationOperation { TargetMethod.Name: LocalizerContract.ResolveMethod } invocation
                when LocalizerContract.Implements(invocation.TargetMethod.ContainingType, contract)
                => new(invocation.Arguments, 0, Resources(invocation, invocation.Instance, generic), KeyedMethod: null),
            IInvocationOperation invocation when LocalizerContract.KeyOrdinal(invocation.TargetMethod) is { } ordinal
                => new(invocation.Arguments, ordinal, (LocalizerContract.ResourceOf(invocation.TargetMethod) is { } resource ? [new LocaleOrigin(resource, $"{invocation.TargetMethod.Name}<{resource.Name}>")] : [], null), invocation.TargetMethod),
            _ => null,
        };

    private static LocalizerLookups Walk(Compilation compilation, SyntaxTree tree, INamedTypeSymbol contract, IEnumerable<SyntaxNode> candidates, Func<IOperation, BoundLookup?> bind)
    {
        var model = compilation.GetSemanticModel(tree);
        var lookups = new List<LocalizerLookup>();
        var unreadable = new List<FileLinePositionSpan>();
        var interpolated = new List<FileLinePositionSpan>();
        var forwarded = new List<ForwardedKey>();

        foreach (var node in candidates)
        {
            // A lookup is read at the node its operation stands on, so a node inside it does not read it again.
            if (model.GetOperation(node) is not { } operation
                || operation.Syntax != node
                || bind(operation) is not { } lookup)
            {
                continue;
            }

            if (SemanticCallsiteReader.Read(operation, lookup.Arguments, lookup.KeyOrdinal) is { } site)
            {
                var resolved = site with { Origins = lookup.Resources.Origins, Receiver = lookup.Resources.Receiver, UsesStringLocalizer = lookup.UsesStringLocalizer };

                lookups.Add(lookup.IsHinted ? resolved with { Arguments = LocalizerArguments.Unknown, IsFormatString = false }
                    : lookup.KeyedMethod is { } method ? Keyed(resolved, method, lookup.KeyOrdinal)
                    : resolved);
            }
            // Microsoft's localizer reads its key as its own contract defines, so only a literal one is read for an entry.
            else if (!lookup.UsesStringLocalizer
                && !LocalizerContract.InsideLocalizer(model.GetEnclosingSymbol(node.SpanStart), contract))
            {
                // A key passed through a parameter is the calls' to write, which are read once every project of the run is loaded.
                if (SemanticCallsiteReader.ForwardedKey(operation, lookup.Arguments, lookup.KeyOrdinal) is { } forwardedKey)
                {
                    forwarded.Add(forwardedKey);
                }
                else if (SemanticCallsiteReader.InterpolatedKey(lookup.Arguments, lookup.KeyOrdinal) is { } key)
                {
                    interpolated.Add(key);
                }
                else if (SemanticCallsiteReader.UnreadableKey(lookup.Arguments, lookup.KeyOrdinal) is { } location)
                {
                    unreadable.Add(location);
                }
            }
        }

        return new LocalizerLookups(lookups, unreadable, interpolated) { ForwardedKeys = forwarded };
    }

    // LocalizableText.For passes the values its params parameter gathers on as a lookup does, and what it returns is text carried as
    // data, never format text.
    private static LocalizerLookup Keyed(LocalizerLookup site, IMethodSymbol method, int keyOrdinal)
        => site with
        {
            Arguments = method.Parameters.Length == keyOrdinal + 2 && method.Parameters[^1].IsParams ? site.Arguments : LocalizerArguments.Unknown,
            IsFormatString = false,
        };

    // The compiler binds a whole member the moment any node in it is asked about, and a lookup is written as an
    // element access or a call named Resolve or For, so only nodes of those shapes are
    // asked and a member holding none is never bound at all.
    private static IEnumerable<SyntaxNode> Candidates(Compilation compilation, SyntaxTree tree)
    {
        var nodes = tree.GetRoot().DescendantNodes();

        return compilation.Language switch
        {
            LanguageNames.CSharp => nodes.Where(IsCSharpCandidate),
            LanguageNames.VisualBasic => nodes.Where(IsVisualBasicCandidate),
            _ => nodes,
        };
    }

    private static IEnumerable<SyntaxNode> KeyedCandidates(Compilation compilation, SyntaxTree tree, IReadOnlySet<string> names)
    {
        var nodes = tree.GetRoot().DescendantNodes();

        return compilation.Language switch
        {
            LanguageNames.CSharp => nodes.Where(node => node is CSharp.InvocationExpressionSyntax invocation && Invoked(invocation.Expression) is { } name && names.Contains(name)),
            LanguageNames.VisualBasic => nodes.Where(IsVisualBasicCandidate),
            _ => nodes,
        };
    }

    private static bool IsCSharpCandidate(SyntaxNode node)
        => node switch
        {
            CSharp.ElementAccessExpressionSyntax or CSharp.ElementBindingExpressionSyntax => true,
            CSharp.InvocationExpressionSyntax invocation => Invoked(invocation.Expression) is LocalizerContract.ResolveMethod or LocalizerContract.LocalizableTextMethod,
            _ => false,
        };

    private static string? Invoked(CSharp.ExpressionSyntax expression)
        => expression switch
        {
            CSharp.MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            CSharp.MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            CSharp.SimpleNameSyntax name => name.Identifier.ValueText,
            _ => null,
        };

    // Visual Basic writes a default property and a call alike, so every call is a candidate.
    private static bool IsVisualBasicCandidate(SyntaxNode node)
        => node is VisualBasic.InvocationExpressionSyntax;

    // The indexer is declared on the unbound contract, so the type it localizes is on a generic receiver, or found by following the
    // receiver once every project of the run is loaded.
    private static (IReadOnlyList<LocaleOrigin> Origins, SyntaxNode? Receiver) Resources(IOperation lookup, IOperation? receiver, INamedTypeSymbol? generic)
        => receiver is null ? ([], null)
            : generic is not null && LocalizerContract.ResourceOf(receiver.Type, generic) is { } resource ? ([new LocaleOrigin(resource, $"IStringLocale<{resource.Name}>")], lookup.Syntax)
            : ([], lookup.Syntax);

    // A lookup as the compiler bound it: its arguments, the ordinal its key binds to, the types its key is read in, the LocalizableText.For
    // or the hinted method a call binds to, and whether it goes through Microsoft's localizer.
    private readonly record struct BoundLookup(
        ImmutableArray<IArgumentOperation> Arguments,
        int KeyOrdinal,
        (IReadOnlyList<LocaleOrigin> Origins, SyntaxNode? Receiver) Resources,
        IMethodSymbol? KeyedMethod,
        bool UsesStringLocalizer = false,
        bool IsHinted = false);

    // Microsoft's localizer and its generic form, where the compilation binds them.
    private readonly record struct StringLocalizerTypes(INamedTypeSymbol? Contract, INamedTypeSymbol? Generic);
}
