namespace Forma.Locali.Flow;

/// <summary>Where a field or a property is written, which decides whether the flow can pair the write with the object it reads.</summary>
internal enum FlowWriteKind
{
    /// <summary>An initializer, run for every object.</summary>
    Initializer,

    /// <summary>A constructor of the member's type, run for the objects it creates.</summary>
    Constructor,

    /// <summary>A <c>??=</c> that caches the first value it is given.</summary>
    LazyInitializer,

    /// <summary>Any other member of the type.</summary>
    Other,

    /// <summary>Code outside the type, which a member it does not keep private lets write it.</summary>
    Outside,
}
