namespace Forma.Locali.Tests.Support;

/// <summary>A directory that exists for one test, under the repository's ignored <c>.sandbox/tmp</c>, and is removed with it.</summary>
public sealed class TempDirectory : IDisposable
{
    private static readonly Lazy<string> TemporaryFolder = new(CreateTemporaryFolder);

    /// <summary>Creates the directory.</summary>
    public TempDirectory()
    {
        Root = Path.Combine(TemporaryFolder.Value, $"forma-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
    }

    /// <summary>Where the directory sits.</summary>
    public string Root { get; }

    /// <summary>Removes the directory and everything written into it, trying again for five seconds while something still holds part of it.</summary>
    /// <remarks>What is still held then stays in <c>.sandbox/tmp</c>, which <c>build.ps1 -Clean</c> removes.</remarks>
    public void Dispose()
    {
        var deadline = Environment.TickCount64 + 5000;

        while (true)
        {
            try
            {
                Directory.Delete(Root, recursive: true);

                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (Environment.TickCount64 >= deadline)
                {
                    return;
                }

                Thread.Sleep(100);
            }
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

    // The folder holds build files of its own, which MSBuild and the compiler read before the repository's, so a project in it loads as it would outside the repository.
    private static string CreateTemporaryFolder()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(repository.FullName, "Forma.Locali.slnx")))
        {
            repository = repository.Parent ?? throw new InvalidOperationException("No Forma.Locali.slnx sits above the tests' output.");
        }

        var folder = Path.Combine(repository.FullName, ".sandbox", "tmp");
        Directory.CreateDirectory(folder);

        WriteOnce(Path.Combine(folder, "Directory.Build.props"), "<Project />");
        WriteOnce(Path.Combine(folder, "Directory.Build.targets"), "<Project />");
        WriteOnce(Path.Combine(folder, ".editorconfig"), "root = true");

        return folder;
    }

    // Written aside and moved into place, so a test run in another process never reads it half-written.
    private static void WriteOnce(string path, string content)
    {
        if (File.Exists(path))
        {
            return;
        }

        var staged = $"{path}.{Guid.NewGuid():N}";
        File.WriteAllText(staged, content);

        try
        {
            File.Move(staged, path);
        }
        catch (IOException)
        {
            File.Delete(staged);
        }
    }
}
