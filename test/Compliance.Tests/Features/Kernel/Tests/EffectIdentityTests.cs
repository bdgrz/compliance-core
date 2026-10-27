using System.Globalization;
using System.Text;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Kernel.Services;
namespace Bdgrz.Compliance.Tests.Features.Kernel.Tests;

public sealed class EffectIdentityTests
{
    static readonly Uuid Source = Uuid.Parse("11111111-2222-3333-4444-555555555555", CultureInfo.InvariantCulture);

    [Fact]
    public void ShouldReturnSameIdGivenSameInputs()
    {
        // Arrange
        // Act
        var first = EffectIdentity.CommandId(Source, "grant", "target-1", "e1");
        var second = EffectIdentity.CommandId(Source, "grant", "target-1", "e1");

        // Assert
        Assert.Equal(first, second);
    }

    [Fact]
    public void ShouldReturnDifferentIdGivenDifferentEpisode()
    {
        // Arrange
        // Act
        var first = EffectIdentity.CommandId(Source, "grant", "target-1", "e1");
        var second = EffectIdentity.CommandId(Source, "grant", "target-1", "e2");

        // Assert
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ShouldUseInitialEpisodeGivenNullEpisode()
    {
        // Arrange
        var expected = Uuid.CreateVersion5(Source, Encoding.UTF8.GetBytes("grant:target-1:initial"));

        // Act
        var actual = EffectIdentity.CommandId(Source, "grant", "target-1", null);

        // Assert
        Assert.Equal(expected, actual);
        Assert.Equal(actual, EffectIdentity.CommandId(Source, "grant", "target-1", "initial"));
    }
}
