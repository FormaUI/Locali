namespace Forma.Locali.Cli;

/// <summary>Turns what the command line named into the files a run reads.</summary>
public interface ILintingTargetResolver
{
    /// <summary>The files the path names, by extension, less the ones the run's configuration excludes.</summary>
    /// <param name="path">The file or directory the command line named.</param>
    LintingTarget Resolve(string path);
}
