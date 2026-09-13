namespace Forma.Locali.Tests.Documents;

using System.Text;

using Forma.Locali.Documents;

public sealed class LocalizationDocumentTests
{
    [Fact]
    public void Should_read_the_entries_inside_a_section_with_its_name()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes(LocalizationFileFakeData.WithSections(("Widget", "Today", "היום"), ("Bars.Toolbar", "Bold", "מודגש")));

        // Act
        var read = LocalizationDocument.TryRead("Scratch.he.json", content, out var document, out _);

        // Assert
        Assert.True(read);
        Assert.Equal(
            [("", "$localization"), ("Widget", "Today"), ("Bars.Toolbar", "Bold")],
            document!.Entries.Select(entry => (entry.Section, entry.Name)));
    }

    [Fact]
    public void Should_name_a_nested_section_by_the_keys_above_it()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("""{ "$localization": "SchemaVersion: 1.0.0; Culture: he;", "Bars": { "Toolbar": { "Bold": "מודגש" } } }""");

        // Act
        LocalizationDocument.TryRead("Scratch.he.json", content, out var document, out _);

        // Assert
        var entry = Assert.Single(document!.Entries, entry => !entry.IsTopLevel);

        Assert.Multiple(
            () => Assert.Equal("Bars.Toolbar", entry.Section),
            () => Assert.Equal("Bold", entry.Name),
            () => Assert.Equal("מודגש", entry.Value));
    }

    [Fact]
    public void Should_number_an_entry_by_the_top_level_property_it_sits_under()
    {
        // Arrange — the section is the file's second property, however many entries it holds.
        var content = Encoding.UTF8.GetBytes(LocalizationFileFakeData.WithSections(("Widget", "Today", "היום"), ("Gallery", "Bold", "מודגש")));

        // Act
        LocalizationDocument.TryRead("Scratch.he.json", content, out var document, out _);

        // Assert
        Assert.Equal([0, 1, 2], document!.Entries.Select(entry => entry.PropertyIndex));
    }

    [Fact]
    public void Should_keep_the_marker_at_the_top_level()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes(LocalizationFileFakeData.Valid);

        // Act
        LocalizationDocument.TryRead("Scratch.he.json", content, out var document, out _);

        // Assert
        Assert.True(document!.Entries[0].IsTopLevel);
    }

    [Fact]
    public void Should_read_the_strings_of_an_array_value()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("""{ "$localization": "SchemaVersion: 1.0.0; Culture: he;", "$tools": { "locali.ignore": ["LOC0113", "LOC0111"], "mixed": ["LOC0113", 1], "next": "x" } }""");

        // Act
        LocalizationDocument.TryRead("Scratch.he.json", content, out var document, out _);

        // Assert
        var entries = document!.Entries.Where(entry => entry.Section == "$tools").ToList();

        Assert.Multiple(
            () => Assert.Equal(["LOC0113", "LOC0111"], entries[0].Items),
            () => Assert.False(entries[0].IsText),
            () => Assert.Null(entries[1].Items),
            () => Assert.Equal(("next", "x"), (entries[2].Name, entries[2].Value)));
    }

    [Fact]
    public void Should_tell_an_entry_under_a_tools_entry_from_a_translation()
    {
        // Arrange — the runtime skips a tools entry wherever it sits.
        var content = Encoding.UTF8.GetBytes("""{ "$localization": "SchemaVersion: 1.0.0; Culture: he;", "$tools": { "crowdin.id": "1" }, "Widget": { "$tools": { "crowdin.id": "2" }, "Today": "היום" } }""");

        // Act
        LocalizationDocument.TryRead("Scratch.he.json", content, out var document, out _);

        // Assert
        var entries = document!.Entries.Where(entry => entry.Name != "$localization").ToList();

        Assert.Equal([true, true, false], entries.Select(entry => entry.IsInsideToolsSection));
    }
}
