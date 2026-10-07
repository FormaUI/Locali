namespace Forma.Locali.Tests.Support.Documents;

using Forma.Framework.Localization.Files;
using Forma.Locali.Documents;

/// <summary>A locale format known only by its extension, which reads no file, so a test can tell which files a run takes for locale files.</summary>
/// <param name="extension">The extension the format's files carry, with its leading dot.</param>
public sealed class StubLocaleFormat(string extension) : ILocaleFormat, ILocaleFileFormat
{
    /// <inheritdoc />
    public ILocaleFileFormat RuntimeFormat
        => this;

    /// <inheritdoc />
    public string Extension { get; } = extension;

    /// <inheritdoc />
    public LocaleFile Read(Stream stream, string fileName, string cultureName)
        => throw new LocaleFileException($"'{fileName}' is not read by a stub format.");

    /// <inheritdoc />
    public bool TryRead(string path, byte[] content, out LocalizationDocument? document, out string? error)
    {
        document = null;
        error = "a stub format reads no file";

        return false;
    }
}
