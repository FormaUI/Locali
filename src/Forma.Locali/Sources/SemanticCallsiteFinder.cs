namespace Forma.Locali.Sources;

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

using CSharp = Microsoft.CodeAnalysis.CSharp.Syntax;
using VisualBasic = Microsoft.CodeAnalysis.VisualBasic.Syntax;

/// <summary>Finds callsites by binding the receiver to the localizer contract.</summary>
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

        return contract is null ? LocalizerLookups.Empty : Walk(compilation, tree, contract);
    }

    // The operation tree is what the compiler bound, in the same shape for every language it reads, so the
    // walk reads it rather than the syntax; an indexer and a default property are one operation there.
    private static (IOperation? Receiver, ImmutableArray<IArgumentOperation> Arguments)? Lookup(IOperation operation, INamedTypeSymbol contract)
        => operation switch
        {
            IPropertyReferenceOperation { Arguments.Length: > 0 } indexer
                when LocalizerContract.Implements(indexer.Property.ContainingType, contract)
                => (indexer.Instance, indexer.Arguments),
            IInvocationOperation { TargetMethod.Name: LocalizerContract.ResolveMethod } invocation
                when LocalizerContract.Implements(invocation.TargetMethod.ContainingType, contract)
                => (invocation.Instance, invocation.Arguments),
            _ => null,
        };

    private static LocalizerLookups Walk(Compilation compilation, SyntaxTree tree, INamedTypeSymbol contract)
    {
        var model = compilation.GetSemanticModel(tree);
        var generic = compilation.GetTypeByMetadataName(LocalizerContract.GenericMetadataName);
        var lookups = new List<LocalizerLookup>();
        var unreadable = new List<FileLinePositionSpan>();
        var interpolated = new List<FileLinePositionSpan>();

        foreach (var node in Candidates(tree.GetRoot(), compilation.Language))
        {
            // A lookup is read at the node its operation stands on, so a node inside it does not read it again.
            if (model.GetOperation(node) is not { } operation
                || operation.Syntax != node
                || Lookup(operation, contract) is not var (receiver, arguments))
            {
                continue;
            }

            if (SemanticCallsiteReader.Read(operation, arguments) is { } site)
            {
                lookups.Add(site with { ResourceType = Resource(receiver, generic) });
            }
            else if (!LocalizerContract.InsideLocalizer(model.GetEnclosingSymbol(node.SpanStart), contract))
            {
                if (SemanticCallsiteReader.InterpolatedKey(arguments) is { } key)
                {
                    interpolated.Add(key);
                }
                else if (SemanticCallsiteReader.UnreadableKey(arguments) is { } location)
                {
                    unreadable.Add(location);
                }
            }
        }

        return new LocalizerLookups(lookups, unreadable, interpolated);
    }

    // The compiler binds a whole member the moment any node in it is asked about, and a lookup is written as an
    // element access or a call named Resolve, so only nodes of those shapes are asked and a member holding neither
    // is never bound at all.
    private static IEnumerable<SyntaxNode> Candidates(SyntaxNode root, string language)
        => language switch
        {
            LanguageNames.CSharp => root.DescendantNodes().Where(IsCSharpCandidate),
            LanguageNames.VisualBasic => root.DescendantNodes().Where(IsVisualBasicCandidate),
            _ => root.DescendantNodes(),
        };

    private static bool IsCSharpCandidate(SyntaxNode node)
        => node switch
        {
            CSharp.ElementAccessExpressionSyntax or CSharp.ElementBindingExpressionSyntax => true,
            CSharp.InvocationExpressionSyntax invocation => Invoked(invocation.Expression) == LocalizerContract.ResolveMethod,
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

    // The indexer is declared on the unbound contract, so the type it translates is only on the receiver.
    private static INamedTypeSymbol? Resource(IOperation? receiver, INamedTypeSymbol? generic)
        => generic is null || receiver is null ? null : LocalizerContract.ResourceOf(receiver.Type, generic);
}
