namespace Forma.Locali.Rules;

using Forma.Locali.Diagnostics;
using Forma.Locali.Projects;
using Forma.Locali.Sources;

using Microsoft.CodeAnalysis;

/// <summary>What a project rule reads, and what it reports through.</summary>
/// <param name="project">The project being checked.</param>
/// <param name="localizationFiles">The locale files the project owns, which the target attributed.</param>
/// <param name="localization">The project's embedded locale files resolved against its types, or null when the project did not compile or is a locale pack.</param>
/// <param name="callsites">The callsites the source pass found.</param>
/// <param name="pack">The locale pack the project is, its folders resolved against the run, or null when the project is not a locale pack or its items were not read.</param>
/// <param name="run">The locale files of every project of the run, which a type of another assembly is read from, or null when the project is read alone.</param>
public sealed class ProjectContext(
    LoadedProject project,
    IReadOnlyList<string> localizationFiles,
    ProjectLocalization? localization,
    IReadOnlyList<LocalizerLookup> callsites,
    LocalePack? pack = null,
    RunLocalizations? run = null)
{
    private readonly List<LintingDiagnostic> _reported = [];

    /// <summary>The project being checked.</summary>
    public LoadedProject Project { get; } = project ?? throw new ArgumentNullException(nameof(project));

    /// <summary>The locale files the project owns, which the target attributed.</summary>
    public IReadOnlyList<string> LocalizationFiles { get; } = localizationFiles ?? throw new ArgumentNullException(nameof(localizationFiles));

    /// <summary>The project's embedded locale files resolved against its types, or null when the project did not compile or is a locale pack.</summary>
    public ProjectLocalization? Localization { get; } = localization;

    /// <summary>The callsites the source pass found.</summary>
    public IReadOnlyList<LocalizerLookup> Callsites { get; } = callsites ?? throw new ArgumentNullException(nameof(callsites));

    /// <summary>The locale pack the project is, its folders resolved against the run, or null when the project is not a locale pack or its items were not read.</summary>
    public LocalePack? Pack { get; } = pack;

    /// <summary>The locale files of every project of the run, which a type of another assembly is read from, or null when the project is read alone.</summary>
    public RunLocalizations? Run { get; } = run;

    /// <summary>The locale files the runtime reads a type from: its own assembly's, where a project of the run builds it, or null where none does.</summary>
    /// <param name="type">The type, as a lookup of this project names it.</param>
    public ProjectLocalization? GetOwnerLocalization(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var assemblyName = type.ContainingAssembly.Name;

        return Localization is { } own && own.AssemblyName == assemblyName ? own : Run?.ForAssembly(assemblyName);
    }

    /// <summary>Everything the rules reported.</summary>
    public IReadOnlyList<LintingDiagnostic> Reported
        => _reported;

    /// <summary>Reports against a whole file.</summary>
    /// <param name="code">The rule reporting.</param>
    /// <param name="path">The file the diagnostic is about.</param>
    /// <param name="message">What is wrong, in a sentence.</param>
    public void Report(LintingCode code, string path, string message)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrEmpty(path);

        _reported.Add(new LintingDiagnostic(path, 1, 1, code.DefaultSeverity, code, message));
    }

    /// <summary>Reports against the key a callsite writes.</summary>
    /// <param name="code">The rule reporting.</param>
    /// <param name="site">The callsite whose key the diagnostic is about.</param>
    /// <param name="message">What is wrong, in a sentence.</param>
    public void Report(LintingCode code, LocalizerLookup site, string message)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(site);

        _reported.Add(new LintingDiagnostic(
            site.Key.Location.Path,
            site.Key.Location.StartLinePosition.Line + 1,
            site.Key.Location.StartLinePosition.Character + 1 + site.Key.TextOffset,
            code.DefaultSeverity,
            code,
            message));
    }
}
