namespace Forma.Locali.Rules;

/// <summary>The entries this linter reads from a file's tools entry, each under a key naming the linter.</summary>
public static class ToolEntries
{
    /// <summary>The name every entry of this linter's starts with, followed by a dot.</summary>
    public const string Tool = "locali";

    /// <summary>The entry listing the rules the file is left out of, as rule identifiers.</summary>
    public const string Ignore = "locali.ignore";
}
