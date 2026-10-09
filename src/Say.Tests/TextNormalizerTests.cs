using Say.Services;

namespace Say.Tests;

public class TextNormalizerTests
{
    [Fact]
    public void Normalize_CollapsesWhitespaceAndTrims()
    {
        Assert.Equal("hello world", TextNormalizer.Normalize("  hello \r\n\t world  ", appendTrailingSpace: false));
    }

    [Fact]
    public void Normalize_AppendsTrailingSpace()
    {
        Assert.Equal("hello world ", TextNormalizer.Normalize("hello world", appendTrailingSpace: true));
    }

    [Fact]
    public void Normalize_DoesNotDoubleTrailingSpace()
    {
        Assert.Equal("hello ", TextNormalizer.Normalize("hello ", appendTrailingSpace: true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n ")]
    public void Normalize_EmptyInput_ReturnsEmpty(string? input)
    {
        Assert.Equal(string.Empty, TextNormalizer.Normalize(input, appendTrailingSpace: true));
    }

    [Fact]
    public void Normalize_MultiLineSpeech_BecomesSingleLine()
    {
        Assert.Equal(
            "first line second line ",
            TextNormalizer.Normalize("first line\nsecond line", appendTrailingSpace: true));
    }
}
