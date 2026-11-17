namespace Forma.Locali.Flow;

/// <summary>What a method does, for one call, with the text the call binds to the parameter a lookup in its body takes as its key.</summary>
internal enum KeyReach
{
    /// <summary>The method reaches the lookup with the parameter holding the call's text, on a path the call's text does not decide.</summary>
    Reads,

    /// <summary>The method never reaches the lookup for the call.</summary>
    Skips,

    /// <summary>Whether the method reaches the lookup, or with what text, turns on the call's text in a way the flow cannot tell.</summary>
    Unknown,
}
