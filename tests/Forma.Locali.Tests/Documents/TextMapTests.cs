namespace Forma.Locali.Tests.Documents;

public sealed class TextMapTests
{
    [Fact]
    public void Should_locate_the_first_character_at_one_one()
    {
        // Arrange
        var map = TextMapTestFactory.For("{\n}");

        // Act
        var (line, column) = map.Locate(0);

        // Assert
        Assert.Equal((1, 1), (line, column));
    }

    [Fact]
    public void Should_count_lines_from_one()
    {
        // Arrange
        var map = TextMapTestFactory.For("a\nbb\nccc");

        // Act
        var (line, column) = map.Locate(5);

        // Assert
        Assert.Equal((3, 1), (line, column));
    }

    [Fact]
    public void Should_count_columns_in_characters_rather_than_bytes()
    {
        // Arrange
        var map = TextMapTestFactory.For("\"שלום\": 1");
        var offset = TextMapTestFactory.OffsetAfter("\"שלום\"");

        // Act
        var (line, column) = map.Locate(offset);

        // Assert
        Assert.Equal((1, 7), (line, column));
    }
}
