namespace Forma.Locali.Documents;

using System.Globalization;

/// <summary>Reads what a localization file's own name says about it.</summary>
public static class LocalizationFileName
{
    /// <summary>The culture the name carries, or null when it carries none.</summary>
    /// <param name="path">The file whose name is read.</param>
    public static string? CultureOf(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var withoutJson = Path.GetFileNameWithoutExtension(path);
        var suffix = Path.GetExtension(withoutJson);

        if (suffix.Length < 2)
        {
            return null;
        }

        try
        {
            return CultureInfo.GetCultureInfo(suffix[1..], predefinedOnly: true).Name;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
