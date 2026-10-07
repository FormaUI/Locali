namespace Forma.Locali.Rules;

using System.Globalization;

using Forma.Framework.Localization;

/// <summary>Finds text shaped like a hole that names nothing a value can be read by.</summary>
internal static class HoleShapes
{
    /// <summary>Every run written as a hole whose name the runtime cannot read.</summary>
    /// <param name="format">The format to read.</param>
    public static List<string> Suspect(string format)
    {
        var suspect = new List<string>();

        for (var index = 0; index < format.Length; index++)
        {
            if (format[index] != '{')
            {
                continue;
            }

            if (index + 1 < format.Length && format[index + 1] == '{')
            {
                // A doubled brace is the written way to keep one as text.
                index++;

                continue;
            }

            var relative = format.AsSpan(index + 1).IndexOf('}');

            if (relative < 0)
            {
                break;
            }

            var close = index + 1 + relative;
            var inner = format[(index + 1)..close];

            if (Unreadable(inner))
            {
                suspect.Add(inner);
            }

            index = close;
        }

        return suspect;
    }

    // Text a reader could have meant as prose is left alone; neither part of a hole carries a space.
    private static bool Intended(string part)
        => part.Length != 0 && !part.Any(char.IsWhiteSpace);

    // The runtime splits on the first colon, then on a comma inside what precedes it.
    private static bool Unreadable(string inner)
    {
        var colon = inner.AsSpan().IndexOf(':');
        var head = colon < 0 ? inner : inner[..colon];
        var comma = head.AsSpan().IndexOf(',');
        var name = comma < 0 ? head : head[..comma];

        if (!Intended(name))
        {
            return false;
        }

        if (!LocaleName.IsValid(name))
        {
            return true;
        }

        // A name the runtime reads still leaves the hole unread when the alignment is not a number.
        var alignment = comma < 0 ? null : head[(comma + 1)..];

        return alignment is not null
            && Intended(alignment)
            && !int.TryParse(alignment, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _);
    }
}
