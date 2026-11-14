namespace Forma.Locali.Sources;

using System.Globalization;

using Forma.Framework.Localization.Files;
using Forma.Locali.Documents;

/// <summary>A project's locale files, its own and every pack's for it, and the types that could read them, with what the runtime's walk answers for each type and culture.</summary>
public sealed class ProjectLocalization
{
    private readonly Dictionary<(string TypeName, string CultureName), IReadOnlyList<LocaleSourceEntry>> _answers = [];
    private readonly Dictionary<string, LocaleSource> _sources = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly Dictionary<string, IReadOnlyList<string>> _namedAssemblies = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly Dictionary<string, BuiltAssembly> _builtAssemblies = [with(StringComparer.Ordinal)];
    private readonly string[] _assemblyNames;
    private readonly Lazy<AssemblyNamedLocaleSource?> _applicationSource;
    private readonly Lazy<HashSet<string>> _nameSegments;

    /// <summary>Resolves the files against the types.</summary>
    /// <param name="assemblyName">The name of the assembly the project builds.</param>
    /// <param name="typeNames">The name of every type the project declares.</param>
    /// <param name="embeddedFiles">The locale files the project embeds.</param>
    /// <param name="source">The embedded files as the runtime's walk reads them.</param>
    /// <param name="packs">Every locale pack's files naming the project's assembly, with the pack read as the runtime reads it for the project.</param>
    /// <param name="isApplication">Whether the project builds an application, whose files the runtime also reads for every assembly their names start with.</param>
    /// <param name="referencedAssemblyNames">The name of every assembly the project references.</param>
    /// <param name="builtAssemblies">Every assembly a project of the run builds, which an application's files are read for as well as its own types.</param>
    /// <param name="format">The format the files are written in.</param>
    public ProjectLocalization(
        string assemblyName,
        IReadOnlyList<string> typeNames,
        IReadOnlyList<ProjectLocaleFile> embeddedFiles,
        ProjectLocaleSource source,
        IReadOnlyList<PackLocalization> packs,
        bool isApplication,
        IReadOnlyCollection<string> referencedAssemblyNames,
        IReadOnlyList<BuiltAssembly> builtAssemblies,
        ILocaleFileFormat format)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyName);
        ArgumentNullException.ThrowIfNull(typeNames);
        ArgumentNullException.ThrowIfNull(embeddedFiles);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(packs);
        ArgumentNullException.ThrowIfNull(referencedAssemblyNames);
        ArgumentNullException.ThrowIfNull(builtAssemblies);
        ArgumentNullException.ThrowIfNull(format);

        AssemblyName = assemblyName;
        IsApplication = isApplication;

        foreach (var builtAssembly in builtAssemblies)
        {
            _builtAssemblies.TryAdd(builtAssembly.AssemblyName, builtAssembly);
        }

        _assemblyNames = [.. referencedAssemblyNames.Concat(_builtAssemblies.Keys).Prepend(assemblyName).Distinct(StringComparer.Ordinal)];
        _applicationSource = new(() => isApplication ? CreateApplicationSource(format) : null);
        _nameSegments = new(() => [.. typeNames.Concat(_assemblyNames).Concat(_builtAssemblies.Values.SelectMany(built => built.TypeNames)).SelectMany(name => name.Split('.'))]);
        TypeNames = typeNames;
        EmbeddedFiles = embeddedFiles;
        Source = source;
        Packs = packs;
        Files = [.. embeddedFiles, .. packs.SelectMany(pack => pack.Files)];
        Cultures = [.. Files.Select(file => file.Culture).DistinctBy(culture => culture.Name)];

        foreach (var file in embeddedFiles)
        {
            _sources[file.Path] = source;
        }

        foreach (var pack in packs)
        {
            foreach (var file in pack.Files)
            {
                _sources[file.Path] = pack.Source;
            }
        }
    }

    /// <summary>The name of the assembly the project builds.</summary>
    public string AssemblyName { get; }

    /// <summary>Whether the project builds an application, whose files the runtime also reads for every assembly their names start with.</summary>
    public bool IsApplication { get; }

    /// <summary>The name of every type the project declares.</summary>
    public IReadOnlyList<string> TypeNames { get; }

    /// <summary>The locale files the project embeds.</summary>
    public IReadOnlyList<ProjectLocaleFile> EmbeddedFiles { get; }

    /// <summary>Every locale pack's files naming the project's assembly, with the pack read as the runtime reads it for the project.</summary>
    public IReadOnlyList<PackLocalization> Packs { get; }

    /// <summary>Every locale file the project's types are read from: the ones it embeds, then each pack's for it.</summary>
    public IReadOnlyList<ProjectLocaleFile> Files { get; }

    /// <summary>Every culture the files are named for.</summary>
    public IReadOnlyList<CultureInfo> Cultures { get; }

    /// <summary>The embedded files as the runtime's walk reads them.</summary>
    public ProjectLocaleSource Source { get; }

    /// <summary>The file the walk's entries report under a name, from the embedded files or any pack's, or null when none answers to it.</summary>
    /// <param name="name">The name, as the walk's entries report it.</param>
    public ProjectLocaleFile? GetFile(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return Source.GetFile(name) ?? Packs.Select(pack => pack.Source.GetFile(name)).FirstOrDefault(file => file is not null);
    }

    /// <summary>The full name of every type or namespace the runtime reads a file for, none when no path in force reads it.</summary>
    /// <param name="file">The file, one of <see cref="Files"/>.</param>
    public IReadOnlyList<string> GetAnchorNames(ProjectLocaleFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return _sources[file.Path].GetAnchorNames(AssemblyName, file.Name, file.Culture.Name);
    }

    /// <summary>The full name of every type or namespace an application reads one of its files for besides those <see cref="GetAnchorNames"/> lists: one for every assembly the file's name starts with; none for a library.</summary>
    /// <param name="file">The file, one of <see cref="Files"/>.</param>
    public IReadOnlyList<string> GetApplicationAnchorNames(ProjectLocaleFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (_sources[file.Path] != Source || _applicationSource.Value is not { } applicationSource || applicationSource.GetName(file) is not { } name)
        {
            return [];
        }

        return [.. GetNamedAssemblies(file).SelectMany(assemblyName => applicationSource.GetAnchorNames(assemblyName, name, file.Culture.Name)).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>The name of every assembly the project builds or references, or a project of the run builds, that an embedded file's name starts with, which an application's files are read for as well as its own types; none for a pack's file.</summary>
    /// <param name="file">The file, one of <see cref="Files"/>.</param>
    public IReadOnlyList<string> GetNamedAssemblies(ProjectLocaleFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (!_namedAssemblies.TryGetValue(file.Path, out var named))
        {
            _namedAssemblies[file.Path] = named = ReadAssemblyNamed(file) is { } fileName
                ? [.. _assemblyNames.Where(assemblyName => fileName.Parts.AsSpan().StartsWith(assemblyName.Split('.')))]
                : [];
        }

        return named;
    }

    /// <summary>Whether the project is an application that may read a file for an assembly whose types are out of reach: one it references and no project of the run builds, or one the run does not know, which the part of its name after every known assembly's could start.</summary>
    /// <param name="file">The file, one of <see cref="Files"/>.</param>
    public bool IsReadOutOfReach(ProjectLocaleFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (!IsApplication || _sources[file.Path] != Source || ReadAssemblyNamed(file) is not { } fileName)
        {
            return false;
        }

        var named = GetNamedAssemblies(file);
        if (named.Any(assemblyName => assemblyName != AssemblyName && !_builtAssemblies.ContainsKey(assemblyName)))
        {
            return true;
        }

        // The name is explained where the part after the longest known assembly's, or after the application's own root, starts a namespace of that assembly's types.
        var assemblyName = named.MaxBy(name => name.Length);
        var rest = fileName.Parts.AsSpan()[(assemblyName?.Split('.').Length ?? 0)..];
        if (rest.IsEmpty)
        {
            return false;
        }

        var (typeNames, rootNamespace) = assemblyName is null || assemblyName == AssemblyName
            ? (TypeNames, Source.GetRootNamespace(AssemblyName))
            : (_builtAssemblies[assemblyName].TypeNames, _builtAssemblies[assemblyName].RootNamespace);
        var start = $"{rootNamespace}.{rest[0]}";

        return !typeNames.Any(typeName => typeName == start || typeName.StartsWith(start + ".", StringComparison.Ordinal));
    }

    /// <summary>Every section an application's reading of its files for the assemblies their names start with reads for a type of one of those assemblies in one culture, whether or not its entries win; none for a library, or for an assembly no project of the run builds.</summary>
    /// <param name="culture">The culture.</param>
    public IReadOnlyList<LocaleSourceSection> GetApplicationSections(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        return [.. ReadApplication(culture, static (entries, typeName) => entries.GetSections(typeName))];
    }

    /// <summary>Every entry two of an application's files give different texts that a lookup for a type of an assembly their names start with meets in one culture; none for a library, or for an assembly no project of the run builds.</summary>
    /// <param name="culture">The culture.</param>
    public IReadOnlyList<LocaleConflict> GetApplicationConflicts(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        return [.. ReadApplication(culture, static (entries, typeName) =>
        {
            var conflicts = new List<LocaleConflict>();
            entries.Collect(typeName, new Dictionary<string, LocaleSourceEntry>(StringComparer.Ordinal), conflicts);

            return conflicts;
        })];
    }

    /// <summary>Whether a part spells a segment of a name the run knows: one of the project's types, an assembly it references or the run builds, or one of the types the run builds.</summary>
    /// <param name="part">The part.</param>
    public bool IsNameSegment(string part)
    {
        ArgumentNullException.ThrowIfNull(part);

        return _nameSegments.Value.Contains(part);
    }

    /// <summary>Whether a file is an application's for another assembly: the longest assembly name its name starts with is not the project's, so it is laid out from that assembly's root namespace.</summary>
    /// <param name="file">The file, one of <see cref="Files"/>.</param>
    public bool IsForAnotherAssembly(ProjectLocaleFile file)
        => IsApplication && GetNamedAssemblies(file).MaxBy(assemblyName => assemblyName.Length) is { } assemblyName && assemblyName != AssemblyName;

    /// <summary>Every entry two files of one source give different texts that a lookup for a type in one culture meets, as the runtime collects the type's entries, from the embedded files and then from each pack in turn.</summary>
    /// <param name="typeName">The type's name, as the runtime resolves it.</param>
    /// <param name="culture">The culture.</param>
    public IReadOnlyList<LocaleConflict> GetConflicts(string typeName, CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrEmpty(typeName);
        ArgumentNullException.ThrowIfNull(culture);

        var conflicts = new List<LocaleConflict>();
        foreach (var source in (LocaleSource[])[Source, .. Packs.Select(pack => pack.Source)])
        {
            source.ReadEntries(AssemblyName, culture.Name).Collect(typeName, new Dictionary<string, LocaleSourceEntry>(StringComparer.Ordinal), conflicts);
        }

        return conflicts;
    }

    /// <summary>Every section the runtime reads for a type in one culture, whether or not its entries win, from the embedded files and then from each pack in turn.</summary>
    /// <param name="typeName">The type's name, as the runtime resolves it.</param>
    /// <param name="culture">The culture.</param>
    public IReadOnlyList<LocaleSourceSection> GetSections(string typeName, CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrEmpty(typeName);
        ArgumentNullException.ThrowIfNull(culture);

        return
        [
            .. Source.ReadEntries(AssemblyName, culture.Name).GetSections(typeName),
            .. Packs.SelectMany(pack => pack.Source.ReadEntries(AssemblyName, culture.Name).GetSections(typeName)),
        ];
    }

    // An application reads its files for other assemblies from where its own names begin, by the default reading whatever paths it states for its own types.
    private LocaleFileName? ReadAssemblyNamed(ProjectLocaleFile file)
    {
        if (_sources[file.Path] != Source)
        {
            return null;
        }

        var namesStart = Source.GetRootDir(AssemblyName) + ".";

        return file.Name.StartsWith(namesStart, StringComparison.Ordinal)
            ? LocalizationFileName.Read(file.Name[namesStart.Length..], Path.GetExtension(file.Name))
            : null;
    }

    // The application's files beneath where its own names begin, read by the names that follow, as the runtime's application source reads them.
    private AssemblyNamedLocaleSource CreateApplicationSource(ILocaleFileFormat format)
    {
        var namesStart = Source.GetRootDir(AssemblyName) + ".";
        var files = EmbeddedFiles
            .Where(file => file.Name.StartsWith(namesStart, StringComparison.Ordinal))
            .Select(file => (file.Name[namesStart.Length..], file));

        return new AssemblyNamedLocaleSource(
            namesStart,
            files,
            assemblyName => assemblyName == AssemblyName ? Source.GetRootNamespace(AssemblyName) : _builtAssemblies.GetValueOrDefault(assemblyName)?.RootNamespace,
            format);
    }

    // Each assembly of the run the application's files name is read for each of its types, the application's own among them.
    private IEnumerable<T> ReadApplication<T>(CultureInfo culture, Func<LocaleEntries, string, IEnumerable<T>> read)
    {
        if (_applicationSource.Value is not { } applicationSource)
        {
            yield break;
        }

        var named = EmbeddedFiles.SelectMany(GetNamedAssemblies).Distinct(StringComparer.Ordinal);
        foreach (var assemblyName in named)
        {
            var typeNames = assemblyName == AssemblyName ? TypeNames : _builtAssemblies.GetValueOrDefault(assemblyName)?.TypeNames ?? [];
            if (typeNames.Count == 0)
            {
                continue;
            }

            var entries = applicationSource.ReadEntries(assemblyName, culture.Name);
            foreach (var typeName in typeNames)
            {
                foreach (var item in read(entries, typeName))
                {
                    yield return item;
                }
            }
        }
    }

    /// <summary>What the runtime reads for a type in one culture, anchor by anchor from the root down to the type, from the embedded files and then from each pack in turn.</summary>
    /// <param name="typeName">The type's name, as the runtime resolves it.</param>
    /// <param name="culture">The culture.</param>
    public IReadOnlyList<LocaleSourceEntry> Read(string typeName, CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrEmpty(typeName);
        ArgumentNullException.ThrowIfNull(culture);

        var key = (typeName, culture.Name);
        if (!_answers.TryGetValue(key, out var entries))
        {
            _answers[key] = entries =
            [
                .. Source.Read(AssemblyName, typeName, culture.Name),
                .. Packs.SelectMany(pack => pack.Source.Read(AssemblyName, typeName, culture.Name)),
            ];
        }

        return entries;
    }
}
