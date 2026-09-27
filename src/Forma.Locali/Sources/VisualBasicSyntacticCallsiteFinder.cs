namespace Forma.Locali.Sources;

using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

/// <summary>Finds callsites in Visual Basic without a semantic model, by tracking which identifiers a type declares as localizers.</summary>
public sealed class VisualBasicSyntacticCallsiteFinder : ILocalizerCallsiteFinder
{
    private readonly ConditionalWeakTable<Compilation, Dictionary<string, HashSet<string>>> _members = [];

    /// <inheritdoc />
    public bool CanRead(Compilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        return compilation.Language == LanguageNames.VisualBasic;
    }

    /// <inheritdoc />
    public LocalizerLookups Find(Compilation compilation, SyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(tree);

        // A partial type's declarations sit in different trees, so the declaration is usually not in the callsite's.
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
            // A default property and a method are both called with parentheses, so the receiver is what tells a lookup.
            (string? receiver, ArgumentListSyntax? arguments) = node switch
            {
                InvocationExpressionSyntax { Expression: IdentifierNameSyntax name, ArgumentList: { } list }
                    => (name.Identifier.ValueText, list),
                InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax name } member,
                    ArgumentList: { } list,
                } when VisualBasicNames.Equals(member.Name.Identifier.ValueText, LocalizerContract.ResolveMethod)
                    => (name.Identifier.ValueText, list),
                _ => (null, null),
            };

            if (receiver is null || arguments is null || !IsLocalizer(node, receiver, members, scopes))
            {
                continue;
            }

            if (VisualBasicSyntacticCallsiteReader.Read(tree, arguments) is { } site)
            {
                lookups.Add(site);
            }
            else if (!InsideLocalizer(node))
            {
                if (VisualBasicSyntacticCallsiteReader.InterpolatedKey(tree, arguments) is { } key)
                {
                    interpolated.Add(key);
                }
                else if (VisualBasicSyntacticCallsiteReader.UnreadableKey(tree, arguments) is { } location)
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

            if (ancestor is TypeBlockSyntax type
                && members.TryGetValue(QualifiedName(type), out var declared)
                && declared.Contains(name))
            {
                return true;
            }
        }

        return false;
    }

    // Each member of a type holds its own locals and parameters, a lambda's among them wherever the lambda is written.
    private static Dictionary<SyntaxNode, HashSet<string>> IndexScopes(SyntaxNode root)
    {
        var scopes = new Dictionary<SyntaxNode, HashSet<string>>();

        foreach (var member in root.DescendantNodes().OfType<TypeBlockSyntax>().SelectMany(type => type.Members))
        {
            // A nested type is a member too, and indexing it would hoist every local in it into one scope.
            if (member is TypeBlockSyntax)
            {
                continue;
            }

            var names = new HashSet<string>(VisualBasicNames.Comparer);
            foreach (var node in member.DescendantNodes())
            {
                switch (node)
                {
                    case VariableDeclaratorSyntax { AsClause: SimpleAsClauseSyntax clause } variable when Names(clause.Type):
                        foreach (var declared in variable.Names)
                        {
                            names.Add(declared.Identifier.ValueText);
                        }

                        break;

                    case ParameterSyntax { AsClause: { } clause } parameter when Names(clause.Type):
                        names.Add(parameter.Identifier.Identifier.ValueText);

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
        var declared = new Dictionary<string, HashSet<string>>(VisualBasicNames.Comparer);

        foreach (var tree in trees)
        {
            foreach (var type in tree.GetRoot().DescendantNodes().OfType<TypeBlockSyntax>())
            {
                var names = Localizers(type);
                if (names.Count == 0)
                {
                    continue;
                }

                var key = QualifiedName(type);
                if (!declared.TryGetValue(key, out var existing))
                {
                    existing = [with(VisualBasicNames.Comparer)];
                    declared[key] = existing;
                }

                existing.UnionWith(names);
            }
        }

        return declared;
    }

    // A property with accessors is a block around its statement, and one without is the statement alone.
    private static HashSet<string> Localizers(TypeBlockSyntax type)
    {
        var names = new HashSet<string>(VisualBasicNames.Comparer);

        foreach (var member in type.Members)
        {
            switch (member)
            {
                case PropertyStatementSyntax { AsClause: SimpleAsClauseSyntax clause } property when Names(clause.Type):
                    names.Add(property.Identifier.ValueText);

                    break;

                case PropertyBlockSyntax { PropertyStatement: { AsClause: SimpleAsClauseSyntax clause } property } when Names(clause.Type):
                    names.Add(property.Identifier.ValueText);

                    break;

                case FieldDeclarationSyntax field:
                    foreach (var declarator in field.Declarators)
                    {
                        if (declarator.AsClause is SimpleAsClauseSyntax { Type: var declaredType } && Names(declaredType))
                        {
                            foreach (var declared in declarator.Names)
                            {
                                names.Add(declared.Identifier.ValueText);
                            }
                        }
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

        return VisualBasicNames.Equals(name, LocalizerContract.TypeName);
    }

    // A type declaring itself a localizer forwards a key rather than writing one, judged without a model.
    private static bool InsideLocalizer(SyntaxNode node)
        => node.Ancestors().OfType<TypeBlockSyntax>().FirstOrDefault() is { } type
            && type.Implements.SelectMany(statement => statement.Types)
                .Concat(type.Inherits.SelectMany(statement => statement.Types))
                .Any(Names);

    private static string QualifiedName(TypeBlockSyntax type)
    {
        var parts = new List<string> { type.BlockStatement.Identifier.ValueText };

        foreach (var ancestor in type.Ancestors())
        {
            switch (ancestor)
            {
                case TypeBlockSyntax outer:
                    parts.Insert(0, outer.BlockStatement.Identifier.ValueText);

                    break;

                case NamespaceBlockSyntax space:
                    parts.Insert(0, space.NamespaceStatement.Name.ToString());

                    break;
            }
        }

        return string.Join('.', parts);
    }
}
