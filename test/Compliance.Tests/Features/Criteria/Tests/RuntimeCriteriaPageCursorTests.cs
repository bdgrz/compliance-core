using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class RuntimeCriteriaPageCursorTests
{
    [Fact]
    public void ShouldPreserveCutAndFilterGivenCursorRoundTrip()
    {
        // Arrange
        var expected = new RuntimeCriteriaPageCursor.Value("entries", Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), "live", "security", "point_of_focus",
            "CC6.1", 19, "opaque-provider-cursor");

        // Act
        var token = RuntimeCriteriaPageCursor.Encode(expected);
        var valid = RuntimeCriteriaPageCursor.TryDecode(token, out var actual);

        // Assert
        Assert.True(valid);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ShouldInvalidateCursorGivenTheEditionRevisionChanges()
    {
        // Arrange
        var cursor = new RuntimeCriteriaPageCursor.Value("entries", Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), "epoch-a", "security", "criterion",
            "", 7, "provider");

        // Act
        var same = RuntimeCriteriaPageCursor.IsCurrent(cursor, "epoch-a", 7);
        var revised = RuntimeCriteriaPageCursor.IsCurrent(cursor, "epoch-a", 8);
        var rebuilt = RuntimeCriteriaPageCursor.IsCurrent(cursor, "epoch-b", 7);

        // Assert
        Assert.True(same);
        Assert.False(revised);
        Assert.False(rebuilt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-cursor")]
    [InlineData("...invalid...")]
    public void ShouldRefuseMalformedCursorGivenUntrustedInput(string token)
    {
        // Arrange
        // Act
        var valid = RuntimeCriteriaPageCursor.TryDecode(token, out _);

        // Assert
        Assert.False(valid);
    }
}
