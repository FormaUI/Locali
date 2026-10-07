namespace Forma.Locali.Rules;

/// <summary>Reads text for the letters that mark it as prose rather than punctuation and values.</summary>
internal static class Words
{
    /// <summary>Whether the span carries at least one letter.</summary>
    /// <param name="text">The text to read.</param>
    public static bool Any(ReadOnlySpan<char> text)
    {
        foreach (var character in text)
        {
            if (char.IsLetter(character))
            {
                return true;
            }
        }

        return false;
    }
}
