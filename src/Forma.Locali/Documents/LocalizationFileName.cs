namespace Forma.Locali.Documents;

using System.Globalization;

/// <summary>Reads what a localization file's own name says about it.</summary>
public static class LocalizationFileName
{
    /// <summary>The culture the name carries, as the part after its last dot or as the whole name before the extension, or null when it carries none.</summary>
    /// <param name="path">The file whose name is read.</param>
    public static string? GetCulture(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var fileName = Path.GetFileNameWithoutExtension(path);
        var suffix = Path.GetExtension(fileName);

        return (suffix.Length > 1 ? FindCulture(suffix[1..]) : null) ?? FindCulture(fileName);
    }

    private static string? FindCulture(string name)
    {
        if (name.Length == 0)
        {
            return null;
        }

        try
        {
            // 'root' names the invariant culture, which no file is named for.
            return CultureInfo.GetCultureInfo(name, predefinedOnly: true).Name is { Length: > 0 } culture ? culture : null;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
