namespace Forma.Locali.Flow;

using Forma.Locali.Configuration;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

/// <summary>A value the flow follows on its way from a lookup to the place its locale's type was decided.</summary>
internal abstract record FlowValue
{
    /// <summary>Whether the value came through alternatives the flow could not pair with the lookup, so it may not be the one read.</summary>
    public bool IsApproximate { get; init; }

    /// <summary>A locale of a type.</summary>
    /// <param name="Type">The type, by its open definition.</param>
    /// <param name="Root">Where the path to the type began.</param>
    internal sealed record Locale(INamedTypeSymbol Type, string Root) : FlowValue
    {
        /// <summary>What established the type.</summary>
        public LocaleEvidence Evidence { get; init; }

        /// <summary>The hint that supplied or verified the type, where one did.</summary>
        public LocaleMemberHint? Hint { get; init; }
    }

    /// <summary>A type, as a value of <see cref="System.Type"/>.</summary>
    /// <param name="Type">The type, by its open definition.</param>
    /// <param name="Root">Where the value was written.</param>
    internal sealed record Named(INamedTypeSymbol Type, string Root) : FlowValue;

    /// <summary>A locale factory.</summary>
    /// <param name="IsRegistered">Whether it is the factory the host registers, which reads the files the run reads.</param>
    /// <param name="Detail">Where the factory came from.</param>
    internal sealed record Factory(bool IsRegistered, string Detail) : FlowValue;

    /// <summary>A delegate: a lambda, a local function or a method group, with the frame it captured.</summary>
    /// <param name="Target">The lambda, the local function or the method reference.</param>
    /// <param name="Closure">The frame the delegate was made in.</param>
    internal sealed record Function(IOperation Target, FlowFrame Closure) : FlowValue;

    /// <summary>A <see cref="Lazy{T}"/>, made with the factory it calls on first use.</summary>
    /// <param name="Producers">What the factory passed to its constructor resolves to.</param>
    internal sealed record Deferred(IReadOnlyList<FlowValue> Producers) : FlowValue;

    /// <summary>An object, made where the flow can see it or by the container.</summary>
    /// <param name="Type">The object's type.</param>
    /// <param name="Creation">The creation that made it, or null when the container made it.</param>
    /// <param name="Frame">The frame the creation ran in, or null when the container made it.</param>
    internal sealed record Allocated(INamedTypeSymbol Type, IObjectCreationOperation? Creation, FlowFrame? Frame) : FlowValue;

    /// <summary>The instance the code runs on, of one of the types it can be.</summary>
    /// <param name="Type">The instance's type.</param>
    internal sealed record Instance(INamedTypeSymbol Type) : FlowValue;

    /// <summary>A service the container gives, known by the interface it is asked for.</summary>
    /// <param name="Interface">The interface.</param>
    internal sealed record Service(INamedTypeSymbol Interface) : FlowValue;

    /// <summary>A path that ended before reaching a value.</summary>
    /// <param name="Kind">Why it ended.</param>
    /// <param name="Detail">Where it ended.</param>
    internal sealed record Gap(LocaleGapKind Kind, string Detail) : FlowValue;
}
