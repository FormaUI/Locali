namespace Forma.Locali.Configuration;

/// <summary>The places a locale file comes from, as the runtime reads them, combined for a run's <c>localeSources</c> entry.</summary>
[Flags]
public enum LocaleSources
{
    /// <summary>No place at all.</summary>
    None = 0,

    /// <summary>A file the assembly embeds in itself.</summary>
    Embedded = 1,

    /// <summary>A file the build moves into one of the assembly's satellite assemblies.</summary>
    Satellite = 2,

    /// <summary>A file a locale pack carries.</summary>
    Pack = 4,

    /// <summary>A file laid in the host's folder beside the application.</summary>
    Host = 8,
}
