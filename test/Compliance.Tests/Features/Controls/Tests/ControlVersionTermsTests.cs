using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Rules;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Controls.Tests;

public sealed class ControlVersionTermsTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("kind", "unclassified")]
    [InlineData("nature", "unknown")]
    [InlineData("cadence", "ad_hoc")]
    [InlineData("subject", "application")]
    [InlineData("kind", "")]
    [InlineData("nature", null)]
    [InlineData("cadence", null)]
    [InlineData("subject", null)]
    public void ShouldRefuseWithoutCreatingControlGivenInvalidVersionTerms(string field, string? value)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var owner = new ControlRules(tenant, ControlRules.IdFor(tenant, program, "TERMS-INVALID"));
        var content = field switch
        {
            "kind" => Content() with { Kind = value! },
            "nature" => Content() with { Nature = value! },
            "cadence" => Content() with { Cadence = value! },
            _ => Content() with { OccurrenceSubject = value! },
        };

        // Act
        var result = owner.Create(program, Uuid.CreateVersion4(), "TERMS-INVALID", content,
            Uuid.CreateVersion4(), "Contributor", Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.False(owner.IsCreated);
        Assert.Empty(owner.AppliedChanges);
    }

    [Theory]
    [InlineData("continuous")]
    [InlineData("daily")]
    [InlineData("weekly")]
    [InlineData("monthly")]
    [InlineData("quarterly")]
    [InlineData("semiannual")]
    [InlineData("annual")]
    [InlineData("event_driven")]
    public void ShouldRetainNormalizedCompleteTermsGivenSupportedCadence(string cadence)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var owner = new ControlRules(tenant, ControlRules.IdFor(tenant, program, "TERMS-VALID"));
        var content = Content() with
        {
            Kind = " DETECTIVE ",
            Nature = " HYBRID ",
            Cadence = " " + cadence.ToUpperInvariant() + " ",
            OccurrenceSubject = " MATERIAL_PROVIDER "
        };

        // Act
        var result = owner.Create(program, Uuid.CreateVersion4(), "TERMS-VALID", content,
            Uuid.CreateVersion4(), "Contributor", Now);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("detective", owner.CurrentContent?.Kind);
        Assert.Equal("hybrid", owner.CurrentContent?.Nature);
        Assert.Equal(cadence, owner.CurrentContent?.Cadence);
        Assert.Equal("material_provider", owner.CurrentContent?.OccurrenceSubject);
    }

    [Theory]
    [InlineData("kind")]
    [InlineData("nature")]
    [InlineData("cadence")]
    [InlineData("subject")]
    public void ShouldRecordRevisionGivenOnlyOneVersionTermChanged(string field)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var owner = new ControlRules(tenant, ControlRules.IdFor(tenant, program, "TERMS-REVISE"));
        var actor = Uuid.CreateVersion4();
        Assert.True(owner.Create(program, Uuid.CreateVersion4(), "TERMS-REVISE", Content(),
            actor, "Contributor", Now).IsSuccess);
        var changed = field switch
        {
            "kind" => Content() with { Kind = "corrective" },
            "nature" => Content() with { Nature = "automated" },
            "cadence" => Content() with { Cadence = "daily" },
            _ => Content() with { OccurrenceSubject = "policy" },
        };

        // Act
        var result = owner.Revise(program, 1, changed, actor, "Contributor", Now.AddMinutes(1));

        // Assert
        Assert.Null(result);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(changed.Kind, owner.CurrentContent?.Kind);
        Assert.Equal(changed.Nature, owner.CurrentContent?.Nature);
        Assert.Equal(changed.Cadence, owner.CurrentContent?.Cadence);
        Assert.Equal(changed.OccurrenceSubject, owner.CurrentContent?.OccurrenceSubject);
    }

    static ControlDraftContent Content() => new("Access review", "Review access", "Check access",
        "An owner reviews access and records the result", ["Dated review record"]);
}
