namespace Forma.Locali.Flow;

/// <summary>Why a path from a lookup to its type ended before reaching one.</summary>
public enum LocaleGapKind
{
    /// <summary>The value is made by code the analysis does not follow.</summary>
    Unsupported,

    /// <summary>The value comes from a member with no source in the run and no compiled form the analysis reads.</summary>
    CompiledCode,

    /// <summary>The locale comes from a factory other than the one the host registers, which may read other files.</summary>
    CustomFactory,

    /// <summary>The locale is named by a string rather than a type.</summary>
    NamedByString,

    /// <summary>An interface call has no one implementation the run's registrations name.</summary>
    Dispatch,

    /// <summary>The value is set by reflection, as a component's parameters are.</summary>
    Reflection,

    /// <summary>A parameter's values come from callers outside the run, or through a delegate.</summary>
    ExternalCallers,

    /// <summary>A local is assigned more than once, or passed by reference.</summary>
    Local,

    /// <summary>A field or property is written where the analysis cannot pair the write with the object read.</summary>
    Write,

    /// <summary>The property a relationship names returns no type the analysis can read.</summary>
    UnreadTypeMember,

    /// <summary>The path returns to a value it is already following.</summary>
    Cycle,

    /// <summary>The path runs deeper than the analysis follows.</summary>
    Depth,
}
