namespace Forma.Locali.Cli;

using System.IO.Enumeration;

/// <summary>Matches a path against a glob, the one dialect every source of one in this tool shares.</summary>
public static class GlobPattern
{
    /// <summary>Whether the path matches the glob: case-insensitive, forward slashes, and a run of one or more <c>*</c> spans separators.</summary>
    /// <param name="pattern">The glob, as a user wrote it.</param>
    /// <param name="path">The path to test, in whatever slash style it was read in.</param>
    public static bool Matches(string pattern, string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);
        ArgumentException.ThrowIfNullOrEmpty(path);

        return FileSystemName.MatchesSimpleExpression(pattern, Normalize(path), ignoreCase: true);
    }

    /// <summary>The path written with forward slashes, so one glob serves either platform.</summary>
    /// <param name="path">The path to rewrite.</param>
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        return path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
