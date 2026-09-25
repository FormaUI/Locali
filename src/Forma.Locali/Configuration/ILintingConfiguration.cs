namespace Forma.Locali.Configuration;

using Forma.Locali.Diagnostics;

/// <summary>The linting configuration a run applies: the one <c>.locali.json</c> nearest above the path the run was given.</summary>
public interface ILintingConfiguration
{
    /// <summary>The <c>.locali.json</c> the run reads, as a full path, or null when none sits at or above the path it was given.</summary>
    string? FilePath { get; }

    /// <summary>Whether the configuration excludes a file from the run.</summary>
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
}
