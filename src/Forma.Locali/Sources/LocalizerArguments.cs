namespace Forma.Locali.Sources;

/// <summary>How a lookup supplies the values its key's holes read.</summary>
public enum LocalizerArguments
{
    /// <summary>The overload could not be resolved, so what the lookup supplies is not known.</summary>
    Unknown,

    /// <summary>The lookup names a key and supplies no value.</summary>
    None,

    /// <summary>The values pair to the key's distinct holes by position.</summary>
    Positional,
}
