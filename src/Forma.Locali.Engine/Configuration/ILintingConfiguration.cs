namespace Forma.Locali.Configuration;

using Forma.Locali.Diagnostics;

/// <summary>The configuration a run applies: the one <c>.locali.json</c> nearest at or above the path the run was given, merged with what the command line excludes.</summary>
public interface ILintingConfiguration
{
    /// <summary>The <c>.locali.json</c> the run reads, as a full path, or null when none sits at or above the path it was given.</summary>
    string? FilePath { get; }

    /// <summary>Whether the configuration excludes a file from the run, by an entry of its file or a glob of its command line.</summary>
    /// <param name="file">The file, as a full path.</param>
    bool IsExcluded(string file);

    /// <summary>The rules the configuration leaves a file out of, empty when it names none.</summary>
    /// <param name="file">The file, as a full path.</param>
    IReadOnlySet<LintingCode> GetIgnoredCodes(string file);

    /// <summary>The sources the last <c>localeSources</c> line matching a file allows it to come from, or null when no line matches it.</summary>
    /// <param name="file">The file, as a full path.</param>
    LocaleSources? GetLocaleSources(string file);

    /// <summary>What the configuration got wrong, and which of its entries applied to no file the run reached.</summary>
    IReadOnlyList<LintingDiagnostic> GetDiagnostics();

    /// <summary>The hints a run reads: the <c>.locali-hints.json</c> of each library it builds or restores, and the configuration's <c>hints</c>, with what is wrong in them.</summary>
    /// <param name="libraries">The folder of each project of the run and of each package its projects restored, with the assemblies each builds or ships.</param>
    LocaleHints ReadHints(IReadOnlyCollection<HintsFolder> libraries);
}
