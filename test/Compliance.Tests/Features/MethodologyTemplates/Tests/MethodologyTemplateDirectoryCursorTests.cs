using System.Globalization;
using System.Text;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class MethodologyTemplateDirectoryCursorTests
{
    [Fact]
    public void ShouldRoundTripExactOrderingKeyGivenNativeProjectionScope()
    {
        // Arrange
        const string projection = "component:bdgrz:methodology-templates:live";
        var ticks = DateTimeOffset.UtcNow.UtcTicks;
        var template = Uuid.CreateVersion4();

        // Act
        var encoded = MethodologyTemplateDirectoryCursor.Encode(projection, ticks, template);
        var valid = MethodologyTemplateDirectoryCursor.TryDecode(projection, encoded, out var readTicks, out var readTemplate);

        // Assert
        Assert.True(valid);
        Assert.Equal(ticks, readTicks);
        Assert.Equal(template, readTemplate);
    }

    [Fact]
    public void ShouldRefuseCursorGivenDifferentProjectionIdentity()
    {
        // Arrange
        var cursor = MethodologyTemplateDirectoryCursor.Encode("live", DateTimeOffset.UtcNow.UtcTicks, Uuid.CreateVersion4());

        // Act
        var accepted = MethodologyTemplateDirectoryCursor.TryDecode("candidate", cursor, out _, out _);

        // Assert
        Assert.False(accepted);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("AAAA")]
    public void ShouldRefuseWithoutThrowingGivenMalformedCursor(string cursor)
    {
        // Arrange
        const string projection = "live";

        // Act
        var valid = MethodologyTemplateDirectoryCursor.TryDecode(projection, cursor, out _, out _);

        // Assert
        Assert.False(valid);
    }

    [Fact]
    public void ShouldRefuseWithoutThrowingGivenNonUtf8Cursor()
    {
        // Arrange
        var cursor = Convert.ToBase64String([0xff]);

        // Act
        var valid = MethodologyTemplateDirectoryCursor.TryDecode("live", cursor, out _, out _);

        // Assert
        Assert.False(valid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(long.MaxValue)]
    public void ShouldRefuseGivenCursorTicksOutsideDateRange(long ticks)
    {
        // Arrange
        var cursor = Convert.ToBase64String(Encoding.UTF8.GetBytes($"cassie-methodology-directory.v1\nlive\n{ticks.ToString(CultureInfo.InvariantCulture)}\n{Uuid.CreateVersion4()}"));

        // Act
        var valid = MethodologyTemplateDirectoryCursor.TryDecode("live", cursor, out _, out _);

        // Assert
        Assert.False(valid);
    }
}
