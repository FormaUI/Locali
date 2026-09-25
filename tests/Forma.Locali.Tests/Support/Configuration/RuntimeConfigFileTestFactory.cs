namespace Forma.Locali.Tests.Support.Configuration;

using System.Text;

using Forma.Locali.Configuration;

/// <summary>Builds the runtime's config file out of text, so a test can read it without touching disk.</summary>
public static class RuntimeConfigFileTestFactory
{
    /// <summary>The file the content parses to.</summary>
    /// <param name="content">The file's text.</param>
    public static RuntimeConfigFile Read(string content)
    {
        ArgumentException.ThrowIfNullOrEmpty(content);

        return RuntimeConfigFile.TryRead(".locale.json", Encoding.UTF8.GetBytes(content), out var file, out var error)
            ? file!
            : throw new InvalidOperationException($"'.locale.json' does not parse as a config file: {error}");
    }
}
