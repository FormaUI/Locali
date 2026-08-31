namespace Forma.Locali.Cli;

/// <summary>Turns what the command line named into the files a run reads.</summary>
public interface ILintTargetResolver
{
    /// <summary>The files the path names, by extension, less the ones a glob excludes.</summary>
    /// <param name="path">The file or directory the command line named.</param>
    /// <param name="excludes">Globs naming files to leave unread, or null to read everything.</param>
    LintTarget Resolve(string path, IReadOnlyCollection<string>? excludes = null);
}
