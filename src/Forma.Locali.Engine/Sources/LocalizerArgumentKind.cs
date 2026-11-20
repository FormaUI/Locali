namespace Forma.Locali.Sources;

/// <summary>How a lookup supplies the values its key's holes and expression arguments read.</summary>
public enum LocalizerArgumentKind
{
    /// <summary>The overload could not be resolved, so what the lookup supplies is not known.</summary>
    Unknown,

    /// <summary>The lookup names a key and supplies no value.</summary>
    None,

    /// <summary>The values pair by position to the key's distinct names, its holes and its expressions' arguments.</summary>
    Positional,
}
