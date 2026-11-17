namespace Forma.Locali.Flow;

/// <summary>What a value computed from a call's arguments holds: a constant where everything it reads is one, and whether it turns on the key the call writes.</summary>
/// <param name="IsConstant">Whether the value is known.</param>
/// <param name="Constant">The value, where it is known.</param>
/// <param name="DependsOnKey">Whether the value is the key or is computed from it.</param>
internal readonly record struct CallValue(bool IsConstant, object? Constant, bool DependsOnKey)
{
    /// <summary>A known value.</summary>
    /// <param name="constant">The value.</param>
    /// <param name="dependsOnKey">Whether it is the key or is computed from it.</param>
    public static CallValue Of(object? constant, bool dependsOnKey)
        => new(true, constant, dependsOnKey);

    /// <summary>A value the flow cannot tell.</summary>
    /// <param name="dependsOnKey">Whether it is the key or is computed from it.</param>
    public static CallValue Unknown(bool dependsOnKey)
        => new(false, null, dependsOnKey);

    /// <summary>The value one of two paths gives: known where both give the same constant.</summary>
    /// <param name="first">What one path gives.</param>
    /// <param name="second">What the other gives.</param>
    public static CallValue Join(CallValue first, CallValue second)
        => first.IsConstant && second.IsConstant && Same(first.Constant, second.Constant)
            ? Of(first.Constant, first.DependsOnKey || second.DependsOnKey)
            : Unknown(first.DependsOnKey || second.DependsOnKey);

    /// <summary>Whether two constants are equal as the runtime compares them: text ordinally, and integers by value whatever their width.</summary>
    /// <param name="first">One constant.</param>
    /// <param name="second">The other.</param>
    public static bool Same(object? first, object? second)
        => (first, second) switch
        {
            (null, null) => true,
            (null, _) or (_, null) => false,
            (string firstText, string secondText) => string.Equals(firstText, secondText, StringComparison.Ordinal),
            _ when Integer(first) is { } firstInteger && Integer(second) is { } secondInteger => firstInteger == secondInteger,
            _ => first.Equals(second),
        };

    /// <summary>An integral constant as a decimal, or null when the constant is not integral.</summary>
    /// <param name="constant">The constant.</param>
    public static decimal? Integer(object? constant)
        => constant switch
        {
            sbyte value => value,
            byte value => value,
            short value => value,
            ushort value => value,
            int value => value,
            uint value => value,
            long value => value,
            ulong value => value,
            char value => value,
            _ => null,
        };
}
