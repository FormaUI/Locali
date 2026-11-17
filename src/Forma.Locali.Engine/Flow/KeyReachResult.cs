namespace Forma.Locali.Flow;

/// <summary>What a method does with one call's key, and, where it reads it, what the target holds when it runs.</summary>
/// <param name="Reach">Whether the method reaches the target with the key the call wrote.</param>
/// <param name="Arguments">What the target, a call passing the key on, passes each parameter of the method it calls, by the parameter's ordinal.</param>
/// <param name="IsKeyShared">Whether the method handed the key, or a value computed from it, to state or code the flow does not follow before reaching the target.</param>
internal sealed record KeyReachResult(KeyReach Reach, IReadOnlyDictionary<int, CallValue> Arguments, bool IsKeyShared)
{
    /// <summary>A method whose path to the target turns on the key in a way the flow cannot tell.</summary>
    public static KeyReachResult Unknown { get; } = new(KeyReach.Unknown, new Dictionary<int, CallValue>(), true);

    /// <summary>A method that never reaches the target for the call.</summary>
    public static KeyReachResult Skips { get; } = new(KeyReach.Skips, new Dictionary<int, CallValue>(), false);
}
