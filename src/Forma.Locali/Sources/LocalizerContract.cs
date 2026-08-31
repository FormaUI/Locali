namespace Forma.Locali.Sources;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Names the localizer contract the linter recognises.</summary>
public static class LocalizerContract
{
    /// <summary>The metadata name of the interface a localization lookup goes through.</summary>
    public const string MetadataName = "Forma.Framework.Localization.IStringLocale";

    /// <summary>The metadata name of the contract that names the type whose translations a lookup reads.</summary>
    public const string GenericMetadataName = "Forma.Framework.Localization.IStringLocale`1";

    /// <summary>The type name as source writes it, without namespace or type arguments.</summary>
    public const string TypeName = "IStringLocale";

    /// <summary>The type whose translations the receiver reads, or null when it names none.</summary>
    /// <param name="receiver">The type the lookup was made through.</param>
    /// <param name="generic">The generic contract as the compilation resolved it.</param>
    public static INamedTypeSymbol? ResourceOf(ITypeSymbol? receiver, INamedTypeSymbol generic)
    {
        ArgumentNullException.ThrowIfNull(generic);

        if (receiver is not INamedTypeSymbol named)
        {
            return null;
        }

        var closed = Closes(named, generic)
            ? named
            : named.AllInterfaces.FirstOrDefault(candidate => Closes(candidate, generic));

        // The factory reads an open definition, because a constructed generic spells its arguments into FullName.
        return closed?.TypeArguments[0] is INamedTypeSymbol resource ? resource.OriginalDefinition : null;
    }

    /// <summary>Whether the type is the localizer contract or implements it.</summary>
    /// <param name="type">The type to judge.</param>
    /// <param name="contract">The contract as the compilation resolved it.</param>
    public static bool Implements(INamedTypeSymbol? type, INamedTypeSymbol contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        if (type is null)
        {
            return false;
        }

        var unbound = type.IsGenericType ? type.OriginalDefinition : type;

        return SymbolEqualityComparer.Default.Equals(unbound, contract)
            || unbound.AllInterfaces.Any(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, contract));
    }

    /// <summary>Whether the type as written names the localizer contract.</summary>
    /// <param name="type">The type syntax to read.</param>
    public static bool Names(TypeSyntax? type)
    {
        var name = type switch
        {
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            _ => null,
        };

        return string.Equals(name, TypeName, StringComparison.Ordinal);
    }

    /// <summary>Whether the node sits inside a localizer, whose lookups forward a key rather than write one.</summary>
    /// <param name="node">The node the lookup was found at.</param>
    /// <param name="model">The model that bound the node.</param>
    /// <param name="contract">The contract as the compilation resolved it.</param>
    public static bool InsideLocalizer(SyntaxNode node, SemanticModel model, INamedTypeSymbol contract)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(contract);

        return Enclosing(node) is { } type
            && model.GetDeclaredSymbol(type) is INamedTypeSymbol symbol
            && Implements(symbol, contract);
    }

    /// <summary>Whether the node sits inside a type declaring itself a localizer, judged without a model.</summary>
    /// <param name="node">The node the lookup was found at.</param>
    public static bool InsideLocalizer(SyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return Enclosing(node)?.BaseList?.Types.Any(declared => Names(declared.Type)) == true;
    }

    private static bool Closes(INamedTypeSymbol type, INamedTypeSymbol generic)
        => type.IsGenericType && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, generic);

    // The member holding the lookup belongs to the nearest type, so an outer one says nothing about it.
    private static TypeDeclarationSyntax? Enclosing(SyntaxNode node)
        => node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
}
