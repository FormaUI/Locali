namespace Forma.Locali.Cli;

using Forma.Locali.Diagnostics;

/// <summary>Reads the arguments the process was started with.</summary>
public static class CommandLine
{
    private const string DefaultReportName = "localization-report.json";

    /// <summary>How to invoke the linter.</summary>
    public static string Usage
        => """
           Usage: locali --path <file-or-directory> [--report [<path>]] [--exclude <glob>]
                         [--severity <level>] [--property <name>=<value>]

             --path     The file or directory to lint. A directory is searched recursively.
             --report   Write a JSON report. Defaults to localization-report.json in the
                        current directory; a path ending in .json names the file, and any
                        other path names the directory to write the default name into.
             --exclude  Leave every file whose path matches the glob unread. May be given
                        more than once. Matching is case-insensitive against the whole
                        path written with forward slashes, and '*' spans separators, so
                        '*/Fixtures/*' skips a folder wherever it sits.
             --severity The lowest severity to report: 'error' or 'warning'. 'warning'
                        includes both warnings and errors. Defaults to 'error'; omitted
                        warnings do not fail the run, and the summary briefs them by rule.
             --property An MSBuild property every project is loaded with, written as
                        'Configuration=Release'. May be given more than once. A project
                        loads as its default build otherwise, so what it compiles or
                        embeds only under another configuration goes unseen.
           """;

    /// <summary>Reads the arguments, reporting the reason rather than throwing when they do not make sense.</summary>
    public static bool TryParse(string[] args, out LinterOptions? options, out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);

        options = null;
        error = null;

        string? path = null;
        string? report = null;
        var wantsReport = false;
        var excludes = new List<string>();
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var severity = LintingSeverity.Error;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--path":
                    if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        error = "'--path' needs a file or directory.";

                        return false;
                    }

                    path = args[++index];

                    break;

                case "--report":
                    wantsReport = true;

                    if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        report = args[++index];
                    }

                    break;

                case "--exclude":
                    if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        error = "'--exclude' needs a glob.";

                        return false;
                    }

                    excludes.Add(args[++index]);

                    break;

                case "--severity":
                    if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        error = "'--severity' needs a level.";

                        return false;
                    }

                    if (ParseSeverity(args[++index]) is not { } level)
                    {
                        error = $"'{args[index]}' is not a severity; use 'error' or 'warning'.";

                        return false;
                    }

                    severity = level;

                    break;

                case "--property":
                    if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        error = "'--property' needs a name and a value, written as 'Configuration=Release'.";

                        return false;
                    }

                    // A property's name carries no '=', so the first one divides it from a value that may carry more.
                    var separator = args[++index].IndexOf('=', StringComparison.Ordinal);
                    if (separator <= 0)
                    {
                        error = $"'{args[index]}' is not a property; write a name and a value, as 'Configuration=Release'.";

                        return false;
                    }

                    properties[args[index][..separator]] = args[index][(separator + 1)..];

                    break;

                default:
                    error = $"'{args[index]}' is not an argument the linter takes.";

                    return false;
            }
        }

        if (path is null)
        {
            error = "'--path' is required.";

            return false;
        }

        options = new LinterOptions
        {
            Path = Path.GetFullPath(path),
            ReportPath = wantsReport ? ResolveReport(report) : null,
            Excludes = excludes,
            Properties = properties,
            MinimumSeverity = severity,
        };

        return true;
    }

    private static LintingSeverity? ParseSeverity(string value)
        => value switch
        {
            _ when value.Equals("error", StringComparison.OrdinalIgnoreCase) => LintingSeverity.Error,
            _ when value.Equals("warning", StringComparison.OrdinalIgnoreCase) => LintingSeverity.Warning,
            _ => null,
        };

    // Every path resolves against the current directory, so a report never lands somewhere the caller did not name.
    private static string ResolveReport(string? report)
    {
        if (report is null)
        {
            return Path.GetFullPath(DefaultReportName);
        }

        return report.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFullPath(report)
            : Path.Combine(Path.GetFullPath(report), DefaultReportName);
    }
}
