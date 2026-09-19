namespace Forma.Locali.Cli;

using Forma.Locali.Diagnostics;

/// <summary>What the command line asked the linter to do.</summary>
public sealed class LinterOptions
{
    /// <summary>The file or directory to lint.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Where to write the JSON report, or null when none was asked for.</summary>
    public string? ReportPath { get; set; }

    /// <summary>The globs naming files the run leaves unread, empty when it reads everything.</summary>
    public IReadOnlyList<string> Excludes { get; set; } = [];

    /// <summary>The MSBuild properties every project is loaded with, empty when each loads as its default build.</summary>
    public IReadOnlyDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

    /// <summary>The lowest severity the run reports; anything below it is counted and dropped.</summary>
    public LintingSeverity MinimumSeverity { get; set; } = LintingSeverity.Error;
}
