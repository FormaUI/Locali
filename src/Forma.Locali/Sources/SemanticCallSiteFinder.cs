namespace Forma.Locali.Sources;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Finds call sites by binding the receiver to the localizer contract.</summary>
public sealed class SemanticCallSiteFinder : ILocalizerCallSiteFinder
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

        return contract is null ? LocalizerLookups.Empty : Walk(compilation, tree, contract);
    }

    private static bool Owns(ISymbol? symbol, INamedTypeSymbol contract)
        => symbol switch
        {
            IPropertySymbol indexer => LocalizerContract.Implements(indexer.ContainingType, contract),
            IMethodSymbol method => method.Name == CallSiteReader.ResolveMethod
                && LocalizerContract.Implements(method.ContainingType, contract),
            _ => false,
        };

    private static LocalizerLookups Walk(Compilation compilation, SyntaxTree tree, INamedTypeSymbol contract)
    {
        var model = compilation.GetSemanticModel(tree);
        var generic = compilation.GetTypeByMetadataName(LocalizerContract.GenericMetadataName);
        var lookups = new List<LocalizerLookup>();
        var unreadable = new List<FileLinePositionSpan>();
        var interpolated = new List<FileLinePositionSpan>();

        foreach (var node in tree.GetRoot().DescendantNodes())
        {
            var arguments = node switch
            {
                ElementAccessExpressionSyntax access => access.ArgumentList,
                InvocationExpressionSyntax invocation => (BaseArgumentListSyntax)invocation.ArgumentList,
                _ => null,
            };

            if (arguments is null)
            {
                continue;
            }

            var symbol = model.GetSymbolInfo(node).Symbol;

            if (!Owns(symbol, contract))
            {
                continue;
            }

            if (CallSiteReader.Read(tree, arguments, symbol, model) is { } site)
            {
                lookups.Add(site with { ResourceType = Resource(model, node, generic) });
            }
            else if (!LocalizerContract.InsideLocalizer(node, model, contract))
            {
                if (CallSiteReader.InterpolatedKey(tree, arguments) is { } key)
                {
                    interpolated.Add(key);
                }
                else if (CallSiteReader.UnreadableKey(tree, arguments) is { } location)
                {
                    unreadable.Add(location);
                }
            }
        }

        return new LocalizerLookups(lookups, unreadable, interpolated);
    }

    // The indexer is declared on the unbound contract, so the type it translates is only on the receiver.
    private static INamedTypeSymbol? Resource(SemanticModel model, SyntaxNode node, INamedTypeSymbol? generic)
    {
        if (generic is null || Receiver(node) is not { } receiver)
        {
            return null;
        }

        return LocalizerContract.ResourceOf(model.GetTypeInfo(receiver).Type, generic);
    }

    private static ExpressionSyntax? Receiver(SyntaxNode node)
        => node switch
        {
            ElementAccessExpressionSyntax access => access.Expression,
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } => member.Expression,
            _ => null,
        };
}
