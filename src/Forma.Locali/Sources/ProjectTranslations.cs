namespace Forma.Locali.Sources;

using System.Globalization;

using Forma.Framework.Localization.Files;

/// <summary>A project's translation files and the types that could read them, with what the runtime's walk answers for each type and culture.</summary>
public sealed class ProjectTranslations
{
    private readonly Dictionary<(string TypeName, string CultureName), IReadOnlyList<LocaleSourceEntry>> _answers = [];

    /// <summary>Resolves the files against the types.</summary>
    /// <param name="assemblyName">The name of the assembly the project builds.</param>
    /// <param name="typeNames">The name of every type the project declares.</param>
    /// <param name="files">The project's translation files.</param>
    /// <param name="format">The format the files are written in.</param>
    public ProjectTranslations(string assemblyName, IReadOnlyList<string> typeNames, IReadOnlyList<TranslationFile> files, ILocaleFileFormat format)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyName);
        ArgumentNullException.ThrowIfNull(typeNames);
        ArgumentNullException.ThrowIfNull(files);

        AssemblyName = assemblyName;
        TypeNames = typeNames;
        Files = files;
        Cultures = [.. files.Select(file => file.Culture).DistinctBy(culture => culture.Name)];
        Source = new ProjectLocaleSource(files, format);
    }

    /// <summary>The name of the assembly the project builds.</summary>
    public string AssemblyName { get; }

    /// <summary>The name of every type the project declares.</summary>
    public IReadOnlyList<string> TypeNames { get; }

    /// <summary>The project's translation files.</summary>
    public IReadOnlyList<TranslationFile> Files { get; }

    /// <summary>Every culture the files are named for.</summary>
    public IReadOnlyList<CultureInfo> Cultures { get; }

    /// <summary>The files as the runtime's walk reads them.</summary>
    public ProjectLocaleSource Source { get; }

    /// <summary>The full name of every type or namespace the runtime reads a file for, none when no path in force reads it.</summary>
    /// <param name="file">The file.</param>
    public IReadOnlyList<string> GetAnchorNames(TranslationFile file)
        => Source.GetAnchorNames(AssemblyName, file);

    /// <summary>What the runtime reads for a type in one culture, anchor by anchor from the root down to the type.</summary>
    /// <param name="typeName">The type's name, as the runtime resolves it.</param>
    /// <param name="culture">The culture.</param>
    public IReadOnlyList<LocaleSourceEntry> Read(string typeName, CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrEmpty(typeName);
        ArgumentNullException.ThrowIfNull(culture);

        var key = (typeName, culture.Name);
        if (!_answers.TryGetValue(key, out var entries))
        {
            _answers[key] = entries = Source.Read(AssemblyName, typeName, culture.Name);
        }

        return entries;
    }
}
