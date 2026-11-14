namespace Forma.Locali.Sources;

using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;

using CSharp = Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Names the methods a C# compilation declares or references whose parameter is marked as a localization key.</summary>
internal static class LocaleKeyMethodNames
{
    private const string AttributeName = "LocaleKey";
    private const string AttributeTypeName = "LocaleKeyAttribute";

    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    // A table runs its factory outside its lock, so each entry is lazy and the trees of a project, walked at once,
    // read it once.
    private static readonly ConditionalWeakTable<Compilation, Lazy<IReadOnlySet<string>>> ByCompilation = [];
    private static readonly ConditionalWeakTable<MetadataReference, Lazy<IReadOnlySet<string>>> ByReference = [];

    /// <summary>The names of the methods the compilation can call whose parameter is marked as a localization key.</summary>
    /// <param name="compilation">The compilation.</param>
    public static IReadOnlySet<string> Get(Compilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        return ByCompilation.GetValue(compilation, key => new(() => Collect(key))).Value;
    }

    private static IReadOnlySet<string> Collect(Compilation compilation)
    {
        // Only an assembly that references the one declaring the attribute can mark a parameter with it.
        if (compilation.GetTypeByMetadataName(LocalizerContract.KeyAttributeMetadataName)?.ContainingAssembly.Name is not { } declaring)
        {
            return None;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tree in compilation.SyntaxTrees)
        {
            CollectDeclared(tree, names);
        }

        // Projects of one run share their references, so each referenced assembly is read once.
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly && References(assembly, declaring))
            {
                names.UnionWith(ByReference.GetValue(reference, _ => new(() => CollectReferenced(assembly))).Value);
            }
        }

        return names;
    }

    private static bool References(IAssemblySymbol assembly, string declaring)
        => assembly.Name == declaring
            || assembly.Modules.Any(module => module.ReferencedAssemblies.Any(identity => identity.Name == declaring));

    // The compilation's own methods are found by their syntax, which names the attribute without binding a member.
    private static void CollectDeclared(SyntaxTree tree, HashSet<string> names)
    {
        foreach (var parameter in tree.GetRoot().DescendantNodes().OfType<CSharp.ParameterSyntax>())
        {
            if (parameter.AttributeLists.Any(list => list.Attributes.Any(IsKeyAttribute)) && DeclaringMethod(parameter) is { } name)
            {
                names.Add(name);
            }
        }
    }

    private static bool IsKeyAttribute(CSharp.AttributeSyntax attribute)
        => UnqualifiedName(attribute.Name) is AttributeName or AttributeTypeName;

    private static string? UnqualifiedName(CSharp.NameSyntax name)
        => name switch
        {
            CSharp.QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            CSharp.AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
            CSharp.SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => null,
        };

    private static string? DeclaringMethod(CSharp.ParameterSyntax parameter)
        => parameter.Parent?.Parent switch
        {
            CSharp.MethodDeclarationSyntax method => method.Identifier.ValueText,
            CSharp.LocalFunctionStatementSyntax function => function.Identifier.ValueText,
            _ => null,
        };

    private static HashSet<string> CollectReferenced(IAssemblySymbol assembly)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        CollectReferenced(assembly.GlobalNamespace, names);

        return names;
    }

    private static void CollectReferenced(INamespaceOrTypeSymbol container, HashSet<string> names)
    {
        foreach (var member in container.GetMembers())
        {
            if (member is INamespaceOrTypeSymbol nested)
            {
                CollectReferenced(nested, names);
            }
            else if (member is IMethodSymbol method && method.Parameters.Any(LocalizerContract.IsKey))
            {
                names.Add(method.Name);
            }
        }
    }
}
