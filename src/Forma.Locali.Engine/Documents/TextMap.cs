namespace Forma.Locali.Documents;

using System.Text;

/// <summary>Turns a byte offset into the line and column an editor would show for it.</summary>
public sealed class TextMap
{
    private readonly int[] _lineStarts;
    private readonly byte[] _utf8;

    /// <summary>Maps positions within one file's UTF-8 bytes.</summary>
    /// <param name="utf8">The file's bytes, without a byte order mark.</param>
    public TextMap(byte[] utf8)
    {
        ArgumentNullException.ThrowIfNull(utf8);

        _utf8 = utf8;

        var starts = new List<int> { 0 };
        for (var index = 0; index < utf8.Length; index++)
        {
            if (utf8[index] == (byte)'\n')
            {
                starts.Add(index + 1);
            }
        }

        _lineStarts = [.. starts];
    }

    /// <summary>The one-based line and column the offset falls on.</summary>
    public (int Line, int Column) Locate(int offset)
    {
        var clamped = Math.Clamp(offset, 0, _utf8.Length);

        var line = Array.BinarySearch(_lineStarts, clamped);
        if (line < 0)
        {
            line = ~line - 1;
        }

        var start = _lineStarts[line];

        // Columns count characters rather than bytes, so a Hebrew line still points where an editor puts the caret.
        var column = Encoding.UTF8.GetCharCount(_utf8, start, clamped - start);

        return (line + 1, column + 1);
    }
}
