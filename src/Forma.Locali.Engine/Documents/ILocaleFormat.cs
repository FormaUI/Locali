namespace Forma.Locali.Documents;

using Forma.Framework.Localization.Files;

/// <summary>A format locale files are written in, read both the way the runtime reads it and the way the file rules read it.</summary>
public interface ILocaleFormat
{
    /// <summary>The runtime's own reader for the format, whose extension names the files written in it.</summary>
    ILocaleFileFormat RuntimeFormat { get; }

    /// <summary>Reads a file's entries in order with their offsets, reporting the reason rather than throwing when the file does not read.</summary>
    /// <param name="path">The file the content was read from.</param>
    /// <param name="content">The file's bytes.</param>
    /// <param name="document">The entries read, or null when the file does not read.</param>
    /// <param name="error">Why the file does not read, or null when it does.</param>
    bool TryRead(string path, byte[] content, out LocalizationDocument? document, out string? error);
}
