namespace Forma.Locali.Configuration;

/// <summary>The name each locale source is written under in a run's <c>localeSources</c> entry, lowercase, as the runtime names the place.</summary>
public static class LocaleSourceNames
{
    private static readonly IReadOnlyList<(string Name, LocaleSources Source)> Spellings =
    [
        ("embedded", LocaleSources.Embedded),
        ("satellite", LocaleSources.Satellite),
        ("pack", LocaleSources.Pack),
        ("host", LocaleSources.Host),
    ];

    /// <summary>Every name, in the order the sources are declared.</summary>
    public static IReadOnlyList<string> All { get; } = [.. Spellings.Select(spelling => spelling.Name)];

    /// <summary>The source a name spells, or null when it spells none.</summary>
    /// <param name="name">The name as written.</param>
    public static LocaleSources? Parse(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        foreach (var (spelling, source) in Spellings)
        {
            if (spelling == name)
            {
                return source;
            }
        }

        return null;
    }

    /// <summary>The names of every source in a combination, in declaration order, joined as a sentence lists them: none as "no source".</summary>
    /// <param name="sources">The sources, combined.</param>
    public static string Format(LocaleSources sources)
    {
        var names = Spellings.Where(spelling => (sources & spelling.Source) != 0).Select(spelling => spelling.Name).ToList();

        return names.Count switch
        {
            0 => "no source",
            1 => names[0],
            _ => $"{string.Join(", ", names[..^1])} and {names[^1]}",
        };
    }
}
