namespace Forma.Locali.Rules;

using Forma.Locali.Diagnostics;
using Forma.Locali.Projects;
using Forma.Locali.Sources;

/// <summary>What a project rule reads, and what it reports through.</summary>
/// <param name="project">The project being checked.</param>
/// <param name="localizationFiles">The locale files the project owns, which the target attributed.</param>
/// <param name="localization">The project's embedded locale files resolved against its types, or null when the project did not compile.</param>
/// <param name="callSites">The call sites the source pass found.</param>
/// <param name="hostFiles">The files of the host's folder at the project's root, with the anchors the runtime places them at.</param>
public sealed class ProjectContext(
    LoadedProject project,
    IReadOnlyList<string> localizationFiles,
    ProjectLocalization? localization,
    IReadOnlyList<LocalizerLookup> callSites,
    IReadOnlyList<PlacedLocaleFile> hostFiles)
{
    private readonly List<LintingDiagnostic> _reported = [];

    /// <summary>The project being checked.</summary>
    public LoadedProject Project { get; } = project ?? throw new ArgumentNullException(nameof(project));

    /// <summary>The locale files the project owns, which the target attributed.</summary>
    public IReadOnlyList<string> LocalizationFiles { get; } = localizationFiles ?? throw new ArgumentNullException(nameof(localizationFiles));

    /// <summary>The project's embedded locale files resolved against its types, or null when the project did not compile.</summary>
    public ProjectLocalization? Localization { get; } = localization;

    /// <summary>The call sites the source pass found.</summary>
    public IReadOnlyList<LocalizerLookup> CallSites { get; } = callSites ?? throw new ArgumentNullException(nameof(callSites));

    /// <summary>The files of the host's folder at the project's root, with the anchors the runtime places them at.</summary>
    public IReadOnlyList<PlacedLocaleFile> HostFiles { get; } = hostFiles ?? throw new ArgumentNullException(nameof(hostFiles));

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

    /// <summary>Reports against the key a call site writes.</summary>
    /// <param name="code">The rule reporting.</param>
    /// <param name="site">The call site whose key the diagnostic is about.</param>
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
