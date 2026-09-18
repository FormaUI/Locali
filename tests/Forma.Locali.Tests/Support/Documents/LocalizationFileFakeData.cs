namespace Forma.Locali.Tests.Support.Documents;

using Forma.Framework.Localization.Files;
/// <summary>Localization file contents a test can lint.</summary>
public static class LocalizationFileFakeData
{
    /// <summary>The name a file carrying Hebrew translations has.</summary>
    public const string HebrewName = "Colocated.he.json";

    /// <summary>A file carrying a translation but no metadata entry.</summary>
    public const string Unmarked = """{ "Today": "היום" }""";

    /// <summary>A file that is not readable as JSON at all.</summary>
    public const string Unreadable = "{ not json";

    /// <summary>A file that follows the schema for Hebrew.</summary>
    public static string Valid { get; } = WithMarker($"SchemaVersion: {LocalizationSchema.CurrentVersion}; Culture: he;");

    /// <summary>A file that follows the schema for Hebrew but declares its metadata entry last.</summary>
    public static string TrailingMarker { get; } = WithTrailingMarker($"SchemaVersion: {LocalizationSchema.CurrentVersion}; Culture: he;");

    /// <summary>A schema-following file for the culture named, carrying one translation.</summary>
    /// <param name="culture">The culture the file declares and is named for.</param>
    /// <param name="key">The key the translation is written under.</param>
    /// <param name="translation">The translation itself.</param>
    public static string ForCulture(string culture, string key, string translation)
        => $$"""
            {
              "{{LocalizationSchema.Marker}}": "SchemaVersion: {{LocalizationSchema.CurrentVersion}}; Culture: {{culture}};",
              "{{key}}": "{{translation}}"
            }
            """;

    /// <summary>A file whose metadata entry declares the fields given.</summary>
    /// <param name="marker">The fields the entry declares.</param>
    public static string WithMarker(string marker)
        => $$"""
            {
              "{{LocalizationSchema.Marker}}": "{{marker}}",
              "Today": "היום"
            }
            """;

    /// <summary>A file that declares its metadata entry after a translation rather than first.</summary>
    /// <param name="marker">The fields the entry declares.</param>
    public static string WithTrailingMarker(string marker)
        => $$"""
            {
              "Today": "היום",
              "{{LocalizationSchema.Marker}}": "{{marker}}"
            }
            """;

    /// <summary>A schema-following file whose metadata entry carries a brace in an optional field.</summary>
    public static string WithBracedMarker { get; } =
        WithMarker($"SchemaVersion: {LocalizationSchema.CurrentVersion}; Culture: he; Author: {{Eyal}};");

    /// <summary>A schema-following file carrying one translation.</summary>
    /// <param name="key">The key the translation is written under.</param>
    /// <param name="translation">The translation itself.</param>
    public static string WithTranslation(string key, string translation)
        => $$"""
            {
              "{{LocalizationSchema.Marker}}": "SchemaVersion: {{LocalizationSchema.CurrentVersion}}; Culture: he;",
              "Today": "היום",
              "{{key}}": "{{translation}}"
            }
            """;

    /// <summary>A schema-following file whose one translation is deliberately the key itself.</summary>
    /// <param name="key">The key left untranslated.</param>
    public static string WithNullTranslation(string key)
        => $$"""
            {
              "{{LocalizationSchema.Marker}}": "SchemaVersion: {{LocalizationSchema.CurrentVersion}}; Culture: he;",
              "Today": "היום",
              "{{key}}": null
            }
            """;

    /// <summary>A schema-following file holding one section, for the type named, with one translation.</summary>
    /// <param name="section">The section, which completes a type's name.</param>
    /// <param name="key">The key the translation is written under.</param>
    /// <param name="translation">The translation itself.</param>
    public static string WithSection(string section, string key, string translation)
        => WithSections((section, key, translation));

    /// <summary>A schema-following file holding one section per type named, each with one translation.</summary>
    /// <param name="sections">The sections, each with the key and translation it holds.</param>
    public static string WithSections(params (string Section, string Key, string Translation)[] sections)
        => $$"""
            {
              "{{LocalizationSchema.Marker}}": "SchemaVersion: {{LocalizationSchema.CurrentVersion}}; Culture: he;",
              {{string.Join(",\n  ", sections.Select(static section => $$"""
                "{{section.Section}}": { "{{section.Key}}": "{{section.Translation}}" }
                """))}}
            }
            """;

    /// <summary>A schema-following file holding one section and one entry outside it.</summary>
    /// <param name="section">The section, which completes a type's name.</param>
    /// <param name="key">The key the section translates.</param>
    /// <param name="translation">The section's translation.</param>
    /// <param name="strayKey">The key written outside the section.</param>
    /// <param name="strayTranslation">The translation written outside the section.</param>
    public static string WithSectionAndStray(string section, string key, string translation, string strayKey, string strayTranslation)
        => $$"""
            {
              "{{LocalizationSchema.Marker}}": "SchemaVersion: {{LocalizationSchema.CurrentVersion}}; Culture: he;",
              "{{section}}": { "{{key}}": "{{translation}}" },
              "{{strayKey}}": "{{strayTranslation}}"
            }
            """;

    /// <summary>A schema-following file whose reserved entries sit in the order given, each named by its key, around one section.</summary>
    /// <param name="order">The top-level keys in the order to write them, the metadata entry among them.</param>
    public static string WithReservedOrder(params string[] order)
    {
        var entries = order.Select(key => key switch
        {
            "$localization" => $"\"{LocalizationSchema.Marker}\": \"SchemaVersion: {LocalizationSchema.CurrentVersion}; Culture: he;\"",
            "$shared" => $"\"{LocalizationSchema.SharedKey}\": {{ \"Bold\": \"מודגש\" }}",
            _ => $"\"{key}\": {{ \"Save\": \"שמור\" }}",
        });

        return "{\n  " + string.Join(",\n  ", entries) + "\n}";
    }

    /// <summary>A schema-following file declaring the same key twice, with different translations.</summary>
    /// <param name="key">The key the file declares more than once.</param>
    public static string WithDuplicateKey(string key)
        => $$"""
            {
              "{{LocalizationSchema.Marker}}": "SchemaVersion: {{LocalizationSchema.CurrentVersion}}; Culture: he;",
              "{{key}}": "ראשון",
              "Today": "היום",
              "{{key}}": "אחרון"
            }
            """;
}
