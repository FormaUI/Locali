namespace Forma.Locali.Tests.Support;

/// <summary>A directory that exists for one test and is removed with it.</summary>
public sealed class TempDirectory : IDisposable
{
    /// <summary>Creates the directory under the system temporary path.</summary>
    public TempDirectory()
    {
        Root = Path.Combine(Path.GetTempPath(), $"forma-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
    }

    /// <summary>Where the directory sits.</summary>
    public string Root { get; }

    /// <summary>Removes the directory and everything written into it.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Writes a file into the directory and returns its full path.</summary>
    /// <param name="name">The file's name, which may name folders to create.</param>
    /// <param name="content">What to write.</param>
    public string Write(string name, string content)
    {
        var file = Path.Combine(Root, name);
        var directory = Path.GetDirectoryName(file);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(file, content);

        return file;
    }
}
