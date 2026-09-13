namespace Forma.Locali.Sources;

using Microsoft.CodeAnalysis;

/// <summary>Names types the way the runtime resolves their translations: containing namespaces and types joined by dots, with no arity.</summary>
public static class ResourceTypeNames
{
    private static readonly SymbolDisplayFormat Format = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.None);

    /// <summary>The name the runtime resolves a type's translations by.</summary>
    /// <param name="type">The type.</param>
    public static string GetName(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.ToDisplayString(Format);
    }

    /// <summary>The name of every type the compilation declares, nested types included.</summary>
    /// <param name="compilation">The compilation.</param>
    public static IReadOnlyList<string> GetAll(Compilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        var names = new List<string>();
        Collect(compilation.Assembly.GlobalNamespace, names);

        return names;
    }

    private static void Collect(INamespaceSymbol container, List<string> names)
    {
        foreach (var type in container.GetTypeMembers())
        {
            Collect(type, names);
        }

        foreach (var child in container.GetNamespaceMembers())
        {
            Collect(child, names);
        }
    }

    private static void Collect(INamedTypeSymbol type, List<string> names)
    {
        names.Add(GetName(type));

        foreach (var nested in type.GetTypeMembers())
        {
            Collect(nested, names);
        }
    }
}
