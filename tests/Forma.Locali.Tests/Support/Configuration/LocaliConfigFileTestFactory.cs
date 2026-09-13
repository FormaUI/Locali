namespace Forma.Locali.Tests.Support.Configuration;

using System.Text;

using Forma.Locali.Configuration;

/// <summary>Builds a linting configuration file out of text, so a test can read it without touching disk.</summary>
public static class LocaliConfigFileTestFactory
{
    /// <summary>The document the content parses to.</summary>
    /// <param name="content">The file's text.</param>
    public static LocaliConfigFile Read(string content)
    {
        ArgumentException.ThrowIfNullOrEmpty(content);

        return LocaliConfigFile.TryRead(".locali.json", Encoding.UTF8.GetBytes(content), out var document, out var error)
            ? document!
            : throw new InvalidOperationException($"'.locali.json' does not parse as a linting configuration: {error}");
    }
}
