namespace Forma.Locali.Sources;

using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Finds callsites without a semantic model, by tracking which identifiers a type declares as localizers.</summary>
public sealed class SyntacticCallsiteFinder : ILocalizerCallsiteFinder
{
    private readonly ConditionalWeakTable<Compilation, Dictionary<string, HashSet<string>>> _members = [];

    /// <inheritdoc />
    public bool CanRead(Compilation compilation)
        => true;

    /// <inheritdoc />
    public LocalizerLookups Find(Compilation compilation, SyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(tree);

        // A component's markup and its code-behind are partials of one type, so the declaration
        // usually sits in a different tree from the callsite.
        var members = _members.GetValue(compilation, static key => IndexMembers(key.SyntaxTrees));
        var root = tree.GetRoot();

        return Walk(tree, root, members, IndexScopes(root));
    }

    private static LocalizerLookups Walk(
        SyntaxTree tree,
        SyntaxNode root,
        IReadOnlyDictionary<string, HashSet<string>> members,
        IReadOnlyDictionary<SyntaxNode, HashSet<string>> scopes)
    {
        var lookups = new List<LocalizerLookup>();
        var unreadable = new List<FileLinePositionSpan>();
        var interpolated = new List<FileLinePositionSpan>();

        foreach (var node in root.DescendantNodes())
        {
            (string? receiver, BaseArgumentListSyntax? arguments) = node switch
            {
                ElementAccessExpressionSyntax { Expression: IdentifierNameSyntax name } access
                    => (name.Identifier.ValueText, (BaseArgumentListSyntax?)access.ArgumentList),
                InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax name } member,
                } invocation when member.Name.Identifier.ValueText == LocalizerContract.ResolveMethod
                    => (name.Identifier.ValueText, invocation.ArgumentList),
                _ => (null, null),
            };

            if (receiver is null || arguments is null || !IsLocalizer(node, receiver, members, scopes))
            {
                continue;
            }

            if (SyntacticCallsiteReader.Read(tree, arguments) is { } site)
            {
                lookups.Add(site);
            }
            else if (!InsideLocalizer(node))
            {
                if (SyntacticCallsiteReader.InterpolatedKey(tree, arguments) is { } key)
                {
                    interpolated.Add(key);
                }
                else if (SyntacticCallsiteReader.UnreadableKey(tree, arguments) is { } location)
                {
                    unreadable.Add(location);
                }
            }
        }

        return new LocalizerLookups(lookups, unreadable, interpolated);
    }

    // A local or parameter shadows the type's own members, so the nearest scope is read first.
    private static bool IsLocalizer(
        SyntaxNode node,
        string name,
        IReadOnlyDictionary<string, HashSet<string>> members,
        IReadOnlyDictionary<SyntaxNode, HashSet<string>> scopes)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (scopes.TryGetValue(ancestor, out var locals) && locals.Contains(name))
            {
                return true;
            }

            if (ancestor is TypeDeclarationSyntax type
                && members.TryGetValue(QualifiedName(type), out var declared)
                && declared.Contains(name))
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<SyntaxNode, HashSet<string>> IndexScopes(SyntaxNode root)
    {
        var scopes = new Dictionary<SyntaxNode, HashSet<string>>();

        foreach (var member in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
        {
            // A namespace and a type are both member declarations, and indexing either would
            // hoist every local in the file into a scope that encloses all of it.
            if (member is TypeDeclarationSyntax or BaseNamespaceDeclarationSyntax)
            {
                continue;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in member.DescendantNodes())
            {
                switch (node)
                {
                    case VariableDeclarationSyntax variable when Names(variable.Type):
                        foreach (var declarator in variable.Variables)
                        {
                            names.Add(declarator.Identifier.ValueText);
                        }

                        break;

                    case ParameterSyntax parameter when Names(parameter.Type):
                        names.Add(parameter.Identifier.ValueText);

                        break;
                }
            }

            if (names.Count != 0)
            {
                scopes[member] = names;
            }
        }

        return scopes;
    }

    private static Dictionary<string, HashSet<string>> IndexMembers(IEnumerable<SyntaxTree> trees)
    {
        var declared = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var tree in trees)
        {
            foreach (var type in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var names = Localizers(type);
                if (names.Count == 0)
                {
                    continue;
                }

                var key = QualifiedName(type);
                if (!declared.TryGetValue(key, out var existing))
                {
                    existing = [with(StringComparer.Ordinal)];
                    declared[key] = existing;
                }

                existing.UnionWith(names);
            }
        }

        return declared;
    }

    private static HashSet<string> Localizers(TypeDeclarationSyntax type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in type.Members)
        {
            switch (member)
            {
                case PropertyDeclarationSyntax property when Names(property.Type):
                    names.Add(property.Identifier.ValueText);

                    break;

                case FieldDeclarationSyntax field when Names(field.Declaration.Type):
                    foreach (var declarator in field.Declaration.Variables)
                    {
                        names.Add(declarator.Identifier.ValueText);
                    }

                    break;
            }
        }

        return names;
    }

    // A qualified spelling still ends in the contract's own name, and a generic one carries it on the name's identifier.
    private static bool Names(TypeSyntax? type)
    {
        var name = type switch
        {
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            _ => null,
        };

        return string.Equals(name, LocalizerContract.TypeName, StringComparison.Ordinal);
    }

    // A type declaring itself a localizer forwards a key rather than writing one, judged without a model.
    private static bool InsideLocalizer(SyntaxNode node)
        => node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.BaseList?.Types.Any(declared => Names(declared.Type)) == true;

    private static string QualifiedName(TypeDeclarationSyntax type)
    {
        var parts = new List<string> { type.Identifier.ValueText };

        foreach (var ancestor in type.Ancestors())
        {
            switch (ancestor)
            {
                case TypeDeclarationSyntax outer:
                    parts.Insert(0, outer.Identifier.ValueText);

                    break;

                case BaseNamespaceDeclarationSyntax space:
                    parts.Insert(0, space.Name.ToString());

                    break;
            }
        }

        return string.Join('.', parts);
    }
}
