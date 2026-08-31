namespace Forma.Locali.Tests.Support.Documents;

using System.Text;

using Forma.Locali.Documents;

/// <summary>Builds maps over text a test wrote inline.</summary>
public static class TextMapTestFactory
{
    /// <summary>The map over the text's UTF-8 bytes.</summary>
    /// <param name="text">The text to map.</param>
    public static TextMap For(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return new TextMap(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>The byte offset the text's UTF-8 encoding puts just after the prefix.</summary>
    /// <param name="prefix">The text preceding the offset.</param>
    public static int OffsetAfter(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        return Encoding.UTF8.GetByteCount(prefix);
    }
}
