namespace Forma.Locali.Sources;

using Microsoft.CodeAnalysis;

/// <summary>The methods a run's hints state look a parameter up as a key, which no project of the run builds: each call to one is a lookup of the key it passes.</summary>
public sealed class KeyedMethods
{
    private readonly Func<IMethodSymbol, int?> _keyOrdinal;

    internal KeyedMethods(IReadOnlySet<string> names, Func<IMethodSymbol, int?> keyOrdinal)
    {
        Names = names;
        _keyOrdinal = keyOrdinal;
    }

    /// <summary>No method at all.</summary>
    public static KeyedMethods None { get; } = new(new HashSet<string>(StringComparer.Ordinal), static _ => null);

    /// <summary>The names the methods go by, which narrow the calls a walk binds.</summary>
    public IReadOnlySet<string> Names { get; }

    /// <summary>Whether there is no method at all.</summary>
    public bool IsEmpty
        => Names.Count == 0;

    /// <summary>The ordinal of the parameter a method looks up as its key, or null where the method is none of these.</summary>
    /// <param name="method">The method a call binds to.</param>
    public int? KeyOrdinal(IMethodSymbol method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return _keyOrdinal(method);
    }
}
