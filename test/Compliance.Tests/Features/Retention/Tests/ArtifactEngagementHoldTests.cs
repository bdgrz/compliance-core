using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Retention.Tests;

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

    [Fact]
    public void ShouldAcceptExactPlacementRetryGivenReportIssuedAfterPlacementWithoutArtifact()
    {
        // Arrange
        var hold = NewHold();
        Assert.True(Place(hold).IsSuccess);
        Assert.True(hold.RecordReportIssued(EngagementId, ReportArtifactId,
            "report-serial-36", Admin, Now.AddDays(30)).IsSuccess);
        var before = NativeFacts.Raised(hold).Length;

        // Act
        var retry = Place(hold);
        var changed = hold.Place(EngagementId, ReportArtifactId, "all examination artifacts", "M0-D16",
            "Preserve evidence through report issuance", Admin, Now);
        var after = NativeFacts.Raised(hold).Length;

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(before, after);
        Assert.Equal(2, after);
    }

    [Fact]
    public void ShouldAppendNothingGivenInvalidPlacementOrIssuance()
    {
        // Arrange
        var hold = NewHold();

        // Act
        var missing = hold.RecordReportIssued(0, EngagementId, ReportArtifactId, "ref", Admin, Now);
        var blankScope = hold.Place(EngagementId, null, "  ", "M0-D16", "reason", Admin, Now);
        var emptyEngagement = hold.Place(Uuid.Empty, null, "scope", "M0-D16", "reason", Admin, Now);
        var factsBeforePlacement = NativeFacts.Raised(hold).Length;
        var placed = Place(hold);
        var otherEngagement = hold.RecordReportIssued(1, Uuid.CreateVersion4(), ReportArtifactId, "ref", Admin,
            Now.AddDays(1));
        var blankReference = hold.RecordReportIssued(1, EngagementId, ReportArtifactId, " ", Admin, Now.AddDays(1));
        var beforePlacement = hold.RecordReportIssued(1, EngagementId, ReportArtifactId, "ref", Admin,
            Now.AddDays(-1));
        var factsAfter = NativeFacts.Raised(hold).Length;

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, missing.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, blankScope.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, emptyEngagement.Error!.Kind);
        Assert.Equal(0, factsBeforePlacement);
        Assert.True(placed.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, otherEngagement.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, blankReference.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, beforePlacement.Error!.Kind);
        Assert.Equal(factsBeforePlacement + 1, factsAfter);
    }

    [Fact]
    public void ShouldRefuseStaleIssuanceAndAcceptOlderRevisionRetryGivenExpectedRevision()
    {
        // Arrange
        var hold = NewHold();
        Assert.True(Place(hold).IsSuccess);
        var issuedAt = Now.AddDays(30);
        var factsBefore = NativeFacts.Raised(hold).Length;

        // Act
        var stale = hold.RecordReportIssued(0, EngagementId, ReportArtifactId, "report-serial-37", Admin, issuedAt);
        var countAfterStale = NativeFacts.Raised(hold).Length;
        var issued = hold.RecordReportIssued(1, EngagementId, ReportArtifactId, "report-serial-37", Admin, issuedAt);
        var retry = hold.RecordReportIssued(1, EngagementId, ReportArtifactId, "report-serial-37", Admin, issuedAt);
        var countAfterRetry = NativeFacts.Raised(hold).Length;

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.Equal(factsBefore, countAfterStale);
        Assert.True(issued.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(factsBefore + 1, countAfterRetry);
        Assert.Equal(2, hold.Revision);
    }

    [Fact]
    public void ShouldRefuseReleaseGivenNoReportIssuedAndRejectInvalidRelease()
    {
        // Arrange
        var hold = NewHold();
        var missing = hold.Release(1, "Org Admin", "reason", Admin, Now);
        Assert.True(Place(hold).IsSuccess);
        var before = NativeFacts.Raised(hold).Length;

        // Act
        var unissued = hold.Release(1, "Org Admin", "reason", Admin, Now.AddYears(5));
        var blankRationale = hold.Release(1, "Org Admin", " ", Admin, Now.AddYears(5));
        var after = NativeFacts.Raised(hold).Length;

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, missing.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, unissued.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, blankRationale.Error!.Kind);
        Assert.Equal(before, after);
        Assert.True(hold.IsActive(new DateOnly(2040, 1, 1)));
    }

    [Fact]
    public void ShouldRefuseStaleReleaseAndKeepReleaseImmutableGivenReleasedHold()
    {
        // Arrange
        var hold = NewHold();
        Assert.True(Place(hold).IsSuccess);
        Assert.True(hold.RecordReportIssued(EngagementId, ReportArtifactId, "report-serial-38", Admin,
            Now.AddDays(1)).IsSuccess);
        var releasedAt = Now.AddYears(1).AddDays(2);

        // Act
        var stale = hold.Release(1, "Org Admin", "Retention window ended", Admin, releasedAt);
        var factsAfterStale = NativeFacts.Raised(hold).Length;
        var released = hold.Release(2, "Org Admin", "Retention window ended", Admin, releasedAt);
        var factsAfterRelease = NativeFacts.Raised(hold).Length;
        var retry = hold.Release(2, "Org Admin", "Retention window ended", Admin, releasedAt);
        var changed = hold.Release(2, "Org Admin", "Different rationale", Admin, releasedAt);
        var reissued = hold.RecordReportIssued(EngagementId, ReportArtifactId, "report-serial-38", Admin,
            Now.AddDays(1));
        var factsAfterRetries = NativeFacts.Raised(hold).Length;

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.Equal(2, factsAfterStale);
        Assert.True(released.IsSuccess);
        Assert.Equal(3, factsAfterRelease);
        Assert.True(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.True(reissued.IsSuccess);
        Assert.Equal(factsAfterRelease, factsAfterRetries);
        Assert.Equal("Retention window ended", hold.Current!.ReleaseRationale);
    }

    [Fact]
    public void ShouldRestoreReleasedHoldAndAcceptRetriesGivenValidHistoryReplay()
    {
        // Arrange
        var source = NewHold();
        Assert.True(Place(source).IsSuccess);
        Assert.True(source.RecordReportIssued(EngagementId, ReportArtifactId, "report-serial-39", Admin,
            Now.AddDays(1)).IsSuccess);
        var releasedAt = Now.AddYears(1).AddDays(2);
        Assert.True(source.Release(2, "Org Admin", "Retention window ended", Admin, releasedAt).IsSuccess);
        var history = NativeFacts.Raised(source);

        // Act
        var replayed = NativeFacts.Replay(NewHold(), history);
        var revision = replayed.Revision;
        var status = replayed.Current!.Status;
        var placeRetry = Place(replayed);
        var releaseRetry = replayed.Release(2, "Org Admin", "Retention window ended", Admin, releasedAt);
        var changed = replayed.Release(2, "Org Admin", "Other", Admin, releasedAt);

        // Assert
        Assert.Equal(3, revision);
        Assert.Equal("released", status);
        Assert.True(placeRetry.IsSuccess);
        Assert.True(releaseRetry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(3, history.Length);
    }

    static Result Place(ArtifactEngagementHold hold) => hold.Place(EngagementId, null, "all examination artifacts",
        "M0-D16", "Preserve evidence through report issuance", Admin, Now);

    static ArtifactEngagementHold NewHold() => new(TenantId, HoldId);
}
