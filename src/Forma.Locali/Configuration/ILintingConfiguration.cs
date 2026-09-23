namespace Forma.Locali.Configuration;

using Forma.Locali.Diagnostics;

/// <summary>The linting configuration, read from every <c>.locali.json</c> a linted file's ancestry carries.</summary>
public interface ILintingConfiguration
{
    /// <summary>Whether a file's own configuration excludes it from the run.</summary>
    /// <param name="file">The file, as a full path.</param>
    bool IsExcluded(string file);

    /// <summary>The rules a file's own configuration leaves it out of, empty when it names none.</summary>
    /// <param name="file">The file, as a full path.</param>
    IReadOnlySet<LintingCode> GetIgnoredCodes(string file);

    /// <summary>The sources the nearest <c>.locali.json</c> naming them allows a file to come from, or null when none in the file's ancestry names them.</summary>
    /// <param name="file">The file, as a full path.</param>
    LocaleSources? GetLocaleSources(string file);

    /// <summary>What every discovered <c>.locali.json</c> got wrong, and which of its globs never matched a file the run reached.</summary>
    IReadOnlyList<LintingDiagnostic> GetDiagnostics();
}
