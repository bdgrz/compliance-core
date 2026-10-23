using Bdgrz.Compliance.Features.Kernel;

namespace Bdgrz.Compliance.Tests.Features.Kernel;

public sealed class StreamKeyOrderTests
{
    [Fact]
    public void ShouldOrderSupplementaryAfterBmpGivenUtf8ByteOrder()
    {
        // Arrange
        const string fullwidthTilde = "～";
        const string grinning = "\U0001F600";

        // Act
        var utf16 = string.CompareOrdinal(fullwidthTilde, grinning);
        var utf8 = StreamKeyOrder.Instance.Compare(fullwidthTilde, grinning);

        // Assert
        Assert.True(utf16 > 0);
        Assert.True(utf8 < 0);
    }

    [Fact]
    public void ShouldSortKeysGivenAsciiAndSupplementary()
    {
        // Arrange
        var keys = new List<string> { "\U0001F600", "b", "a", "～" };

        // Act
        keys.Sort(StreamKeyOrder.Instance);

        // Assert
        Assert.Equal(["a", "b", "～", "\U0001F600"], keys);
    }
}
