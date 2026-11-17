namespace Forma.Locali.Sources;

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

/// <summary>Reads the key out of a bound lookup's arguments, mapped back to the file a human wrote.</summary>
internal static class SemanticCallsiteReader
{
    private const string FormatMethod = "Format";
    private const string FormatParameter = "format";

    /// <summary>The lookup its arguments describe, or null when the key is not readable text.</summary>
    /// <param name="lookup">The lookup operation.</param>
    /// <param name="arguments">The arguments the lookup bound.</param>
    /// <param name="keyOrdinal">The ordinal of the parameter the key binds to, whose following parameters take the values.</param>
    public static LocalizerLookup? Read(IOperation lookup, ImmutableArray<IArgumentOperation> arguments, int keyOrdinal)
    {
        // The compiler folds a constant, so a key written as one is as readable as a literal spelled out.
        if (Key(arguments, keyOrdinal)?.Value is not { ConstantValue: { HasValue: true, Value: string text } } value)
        {
            return null;
        }

        // The span covers a reference rather than a quoted literal, so the key starts where the span does.
        var offset = Unconverted(value) is ILiteralOperation literal ? Offset(literal) : 0;

        return new LocalizerLookup(new LocalizationKeyReference(text, Span(value), offset), Supplies(arguments, keyOrdinal), IsFormatString(lookup));
    }

    /// <summary>Where a lookup writes its key as an interpolated string with holes, or null when it does not.</summary>
    /// <param name="arguments">The arguments the lookup bound.</param>
    /// <param name="keyOrdinal">The ordinal of the parameter the key binds to.</param>
    public static FileLinePositionSpan? InterpolatedKey(ImmutableArray<IArgumentOperation> arguments, int keyOrdinal)
        => Key(arguments, keyOrdinal)?.Value is { } value
            && Unconverted(value) is IInterpolatedStringOperation interpolated
            && interpolated.Parts.OfType<IInterpolationOperation>().Any()
            ? Span(value)
            : null;

    /// <summary>Where a lookup writes the key that could not be read, or null when it writes no key at all.</summary>
    /// <param name="arguments">The arguments the lookup bound.</param>
    /// <param name="keyOrdinal">The ordinal of the parameter the key binds to.</param>
    public static FileLinePositionSpan? UnreadableKey(ImmutableArray<IArgumentOperation> arguments, int keyOrdinal)
        => Key(arguments, keyOrdinal) is { } key ? Span(key) : null;

    /// <summary>The key a lookup takes from a parameter of its own method, whose calls write it, or null when the key is anything else.</summary>
    /// <param name="lookup">The lookup operation.</param>
    /// <param name="arguments">The arguments the lookup bound.</param>
    /// <param name="keyOrdinal">The ordinal of the parameter the key binds to.</param>
    public static ForwardedKey? ForwardedKey(IOperation lookup, ImmutableArray<IArgumentOperation> arguments, int keyOrdinal)
        => Key(arguments, keyOrdinal)?.Value is { } value
            && Unconverted(value) is IParameterReferenceOperation { Parameter: { ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.Ordinary } } parameter }
            ? new ForwardedKey(lookup.Syntax, Span(value), parameter.Name)
            : null;

    // The key binds to one parameter, wherever a named argument writes it.
    private static IArgumentOperation? Key(ImmutableArray<IArgumentOperation> arguments, int keyOrdinal)
        => arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == keyOrdinal);

    // A value passed alongside the format text is no less an argument to string.Format, so the parameter the
    // lookup actually bound to is what decides this, not where it sits in the source; a named argument can
    // reorder them freely.
    private static bool IsFormatString(IOperation lookup)
    {
        var parent = lookup.Parent;
        while (parent is IConversionOperation { IsImplicit: true })
        {
            parent = parent.Parent;
        }

        return parent is IArgumentOperation
        {
            Parameter.Name: FormatParameter,
            Parent: IInvocationOperation { TargetMethod: { Name: FormatMethod, ContainingType.SpecialType: SpecialType.System_String } },
        };
    }

    private static LocalizerArguments Supplies(ImmutableArray<IArgumentOperation> arguments, int keyOrdinal)
    {
        var values = arguments.Where(argument => argument.Parameter?.Ordinal > keyOrdinal).ToArray();
        if (values.Length == 0)
        {
            return LocalizerArguments.None;
        }

        // The compiler gathers what was written for a params parameter into one argument of its own, so the
        // values are counted inside it; passing the collection itself rather than its items leaves nothing to count.
        var last = values.MaxBy(argument => argument.Parameter!.Ordinal)!;
        var gathered = last.ArgumentKind switch
        {
            ArgumentKind.ParamArray when last.Value is IArrayCreationOperation array => array.Initializer?.ElementValues.Length ?? 0,
            ArgumentKind.ParamCollection when last.Value is ICollectionExpressionOperation collection => collection.Elements.Length,
            _ => (int?)null,
        };

        if (gathered is not { } count)
        {
            return LocalizerArguments.Unknown;
        }

        var written = values.Length - 1 + count;
        if (written == 0)
        {
            return LocalizerArguments.None;
        }

        return Element(last.Parameter!.Type)?.SpecialType is SpecialType.System_Object
            ? LocalizerArguments.Positional(written)
            : LocalizerArguments.Unknown;
    }

    private static ITypeSymbol? Element(ITypeSymbol type)
        => type switch
        {
            IArrayTypeSymbol array => array.ElementType,
            INamedTypeSymbol { TypeArguments.Length: 1 } named => named.TypeArguments[0],
            _ => null,
        };

    // A conversion the compiler inserted stands on the operand's own syntax, so the operand is what was written.
    private static IOperation Unconverted(IOperation operation)
    {
        while (operation is IConversionOperation { IsImplicit: true } conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    private static FileLinePositionSpan Span(IOperation operation)
        => operation.Syntax.GetLocation().GetMappedLineSpan();

    // The key's text starts after the opening quote, which a raw or verbatim literal makes more than one character.
    private static int Offset(ILiteralOperation literal)
    {
        var written = literal.Syntax.ToString();

        return written.Length - written.TrimStart('@', '"').Length;
    }
}
