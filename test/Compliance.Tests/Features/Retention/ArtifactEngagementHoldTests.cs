using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Retention;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Retention;

public sealed class ArtifactEngagementHoldTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid HoldId = Uuid.CreateVersion4();
    static readonly Uuid EngagementId = Uuid.CreateVersion4();
    static readonly Uuid ReportArtifactId = Uuid.CreateVersion4();
    static readonly ActorReference Admin = ActorReference.ForMember(Uuid.CreateVersion4(), "Org Admin");
    static readonly DateTimeOffset Now = new(2026, 10, 17, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRetainIndefiniteHoldUntilReportIsIssuedGivenPlacement()
    {
        // Arrange
        var hold = NewHold();

        // Act
        var placed = hold.Place(EngagementId, null, "all examination artifacts", "M0-D16",
            "Preserve evidence through report issuance", Admin, Now);
        var activeBeforeReport = hold.IsActive(new DateOnly(2036, 10, 17));

        // Assert
        Assert.True(placed.IsSuccess);
        Assert.True(activeBeforeReport);
        Assert.Equal("active", hold.Current!.Status);
        Assert.Equal(EngagementId, hold.Current.EngagementId);
        Assert.Single(new AggregateScenario<ArtifactEngagementHold>(hold).PendingEvents);
    }

    [Fact]
    public void ShouldKeepHoldActiveThroughOneYearAfterReportGivenReportIssuance()
    {
        // Arrange
        var hold = NewHold();
        Assert.True(hold.Place(EngagementId, null, "all examination artifacts", "M0-D16",
            "Preserve evidence through report issuance", Admin, Now).IsSuccess);

        // Act
        var issued = hold.RecordReportIssued(EngagementId, ReportArtifactId,
            "report-serial-33", Admin, Now.AddDays(30));
        var beforeExpiry = hold.IsActive(new DateOnly(2027, 11, 15));
        var atExpiry = hold.IsActive(new DateOnly(2027, 11, 16));

        // Assert
        Assert.True(issued.IsSuccess);
        Assert.True(beforeExpiry);
        Assert.False(atExpiry);
        Assert.Equal(new DateOnly(2027, 11, 16), hold.Current!.ReleaseEligibleOn);
        Assert.Equal("report-serial-33", hold.Current.IssuerReference);
    }

    [Fact]
    public void ShouldRequireElapsedHoldAndPreserveReleaseGivenRelease()
    {
        // Arrange
        var hold = NewHold();
        Assert.True(hold.Place(EngagementId, ReportArtifactId, "all examination artifacts", "M0-D16",
            "Preserve evidence through report issuance", Admin, Now).IsSuccess);
        Assert.True(hold.RecordReportIssued(EngagementId, ReportArtifactId,
            "report-serial-34", Admin,
            new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero)).IsSuccess);

        // Act
        var early = hold.Release(2, "Org Admin", "Report retention window ended", Admin,
            new DateTimeOffset(2027, 11, 15, 0, 0, 0, TimeSpan.Zero));
        var released = hold.Release(2, "Org Admin", "Report retention window ended", Admin,
            new DateTimeOffset(2027, 11, 16, 0, 0, 0, TimeSpan.Zero));
        var replay = new AggregateScenario<ArtifactEngagementHold>(NewHold()).Given(
            new AggregateScenario<ArtifactEngagementHold>(hold).PendingEvents.ToArray()).Aggregate;

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(early.Error).Kind);
        Assert.True(released.IsSuccess);
        Assert.Equal("released", hold.Current!.Status);
        Assert.False(hold.IsActive(new DateOnly(2027, 1, 1)));
        Assert.Equal(hold.Current.Status, replay.Current!.Status);
        Assert.Equal(hold.Current.ReleasedAt, replay.Current.ReleasedAt);
    }

    [Fact]
    public void ShouldRejectChangedHoldIdentityAndSecondReportIssuanceGivenRetry()
    {
        // Arrange
        var hold = NewHold();
        Assert.True(hold.Place(EngagementId, null, "all examination artifacts", "M0-D16",
            "Preserve evidence through report issuance", Admin, Now).IsSuccess);
        Assert.True(hold.RecordReportIssued(EngagementId, ReportArtifactId,
            "report-serial-35", Admin, Now.AddDays(30)).IsSuccess);

        // Act
        var changed = hold.Place(EngagementId, null, "single request only", "M0-D16",
            "Changed scope", Admin, Now);
        var changedReport = hold.RecordReportIssued(EngagementId, Uuid.CreateVersion4(),
            "different-report", Admin, Now.AddDays(31));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changed.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changedReport.Error).Kind);
        Assert.Equal(ReportArtifactId, hold.Current!.ReportArtifactId);
    }

    static ArtifactEngagementHold NewHold() => new(TenantId, HoldId);
}
