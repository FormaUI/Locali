namespace Forma.Locali.Cli;

/// <summary>What the process reports about the run.</summary>
public enum ExitCode
{
    /// <summary>Every file linted clean.</summary>
    Clean = 0,

    /// <summary>At least one diagnostic was reported.</summary>
    Diagnostics = 1,

    /// <summary>The command line was wrong, or a path could not be read.</summary>
    Usage = 2,
}
