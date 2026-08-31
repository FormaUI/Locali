namespace Forma.Locali.Sources;

/// <summary>Names a translation file could be embedded under, given where it sits in its project.</summary>
internal static class ResourceBaseNames
{
    // A generic's metadata name carries its arity, which the file name drops, so each plausible one is tried.
    private const int HighestArity = 8;

    /// <summary>Every full name the file's own location could imply, the conventional one first.</summary>
    /// <param name="root">The namespace the project embeds its resources under.</param>
    /// <param name="projectDirectory">The folder the project file sits in.</param>
    /// <param name="filePath">The translation file to name.</param>
    // The SDK embeds a file as {RootNamespace}.{RelativeFolderPath}.{Name}, dropping the culture segment for
    // a satellite resource, so the type a file serves is named for its folder position, not its culture suffix.
    // ResourcesPath then inserts leading folder segments that no project file records, so each may be shed in
    // turn; a type in the global namespace carries no root at all.
    public static IReadOnlyList<string> Candidates(string root, string projectDirectory, string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        ArgumentException.ThrowIfNullOrEmpty(projectDirectory);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var directory = Path.GetDirectoryName(filePath)!;
        var relative = Path.GetRelativePath(projectDirectory, directory);
        string[] segments = relative is "." ? [] : relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var withoutJson = Path.GetFileNameWithoutExtension(filePath);
        var baseName = Path.GetFileNameWithoutExtension(withoutJson);

        var rooted = Enumerable
            .Range(0, segments.Length + 1)
            .Select(dropped => string.Join('.', new[] { root }.Concat(segments[dropped..]).Append(baseName)));

        return [.. rooted.Append(baseName).SelectMany(WithArities)];
    }

    private static IEnumerable<string> WithArities(string name)
        => [name, .. Enumerable.Range(1, HighestArity).Select(arity => $"{name}`{arity}")];
}
