using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Retention;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Retention;

public sealed class ArtifactDispositionTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ArtifactId = Uuid.CreateVersion4();
    static readonly Uuid DispositionId = Uuid.CreateVersion4();
    static readonly ActorReference Admin = ActorReference.ForMember(Uuid.CreateVersion4(), "Org Admin");
    static readonly ActorReference Worker = ActorReference.ForSystemProcess(
        "reactor:artifact-disposition-worker", "Artifact disposition worker");
    static readonly DateTimeOffset Now = new(2026, 10, 17, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRequireElapsedRetentionAndNoHoldsOrRelianceGivenApproval()
    {
        // Arrange
        var disposition = NewDisposition();

        // Act
        var anniversary = disposition.Approve(Eligible() with { AsOf = new DateOnly(2026, 12, 31) },
            0, "Retention elapsed", Admin, Now);
        var held = disposition.Approve(Eligible() with { ActiveHoldIds = [Uuid.CreateVersion4()] },
            0, "Retention elapsed", Admin, Now);
        var reliedUpon = disposition.Approve(Eligible() with
        {
            HistoricalRelianceIds = [Uuid.CreateVersion4()]
        }, 0, "Retention elapsed", Admin, Now);
        var approved = disposition.Approve(Eligible(), 0, "No active hold or reliance", Admin, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(anniversary.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(held.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(reliedUpon.Error).Kind);
        Assert.True(approved.IsSuccess);
        Assert.Equal("approved", disposition.Current!.Status);
        Assert.Equal(new DateOnly(2026, 12, 31), disposition.Current.RetainsThrough);
    }

    [Fact]
    public void ShouldRetainFailedAttemptAndCreateTombstoneOnlyAfterSuccessfulDispositionGivenExecution()
    {
        // Arrange
        var disposition = NewDisposition();
        Assert.True(disposition.Approve(Eligible(), 0, "No active hold or reliance", Admin, Now).IsSuccess);
        Assert.True(disposition.RequestExecution(disposition.Revision, Eligible(), Worker,
            Now.AddHours(1)).IsSuccess);

        // Act
        var failed = disposition.RecordExecutionResult(disposition.Revision, "failed",
            "storage_timeout", Worker, Now.AddHours(2));
        var noTombstoneOnFailure = disposition.Current!.Tombstone;
        var retried = disposition.RequestExecution(disposition.Revision, Eligible(), Worker,
            Now.AddHours(3));
        var disposed = disposition.RecordExecutionResult(disposition.Revision, "disposed", null,
            Worker, Now.AddHours(4));

        // Assert
        Assert.True(failed.IsSuccess);
        Assert.Null(noTombstoneOnFailure);
        Assert.True(retried.IsSuccess);
        Assert.True(disposed.IsSuccess);
        Assert.Equal("disposed", disposition.Current!.Status);
        Assert.Equal(2, disposition.Current.Attempts.Count);
        Assert.Equal("failed", disposition.Current.Attempts[0].Outcome);
        Assert.Equal(ArtifactId, disposition.Current.Tombstone!.ArtifactId);
        Assert.Equal(Eligible().Source.ContentSha256,
            disposition.Current.Tombstone.ContentSha256);
        Assert.Equal(Eligible().ContentLength, disposition.Current.Tombstone.ContentLength);
    }

    [Fact]
    public void ShouldRecheckCurrentHoldsAndSourceBeforeExecutionGivenApprovedDisposition()
    {
        // Arrange
        var disposition = NewDisposition();
        Assert.True(disposition.Approve(Eligible(), 0, "Retention elapsed", Admin, Now).IsSuccess);

        // Act
        var newHold = disposition.RequestExecution(disposition.Revision,
            Eligible() with { ActiveHoldIds = [Uuid.CreateVersion4()] }, Worker, Now.AddHours(1));
        var changedSource = disposition.RequestExecution(disposition.Revision,
            Eligible() with
            {
                Source = Eligible().Source with { ContentSha256 = new string('b', 64) }
            }, Worker, Now.AddHours(1));
        var secret = disposition.Approve(Eligible() with
        {
            ArtifactState = "rejected",
            StateReason = "secret_detected"
        }, 0, "Never store secrets", Admin, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(newHold.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changedSource.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(secret.Error).Kind);
        Assert.Equal(1, disposition.Revision);
    }

    [Fact]
    public void ShouldReplayApprovalAttemptsAndTombstoneGivenCompletedDisposition()
    {
        // Arrange
        var disposition = NewDisposition();
        Assert.True(disposition.Approve(Eligible(), 0, "Retention elapsed", Admin, Now).IsSuccess);
        Assert.True(disposition.RequestExecution(disposition.Revision, Eligible(), Worker,
            Now.AddHours(1)).IsSuccess);
        Assert.True(disposition.RecordExecutionResult(disposition.Revision, "disposed", null,
            Worker, Now.AddHours(2)).IsSuccess);
        var events = new AggregateScenario<ArtifactDisposition>(disposition).PendingEvents.ToArray();

        // Act
        var replay = new AggregateScenario<ArtifactDisposition>(NewDisposition()).Given(events).Aggregate;

        // Assert
        Assert.Equal("disposed", replay.Current!.Status);
        Assert.Equal(disposition.Current!.Revision, replay.Revision);
        Assert.Equal(disposition.Current.Tombstone, replay.Current.Tombstone);
        Assert.Equal(disposition.Current.Attempts, replay.Current.Attempts);
    }

    static ArtifactDisposition NewDisposition() => new(TenantId, DispositionId);

    static ArtifactDispositionEligibilitySnapshot Eligible() => new(
        new ArtifactRetentionSource(TenantId, "evidence_artifact", ArtifactId,
            new string('a', 64)), 12, 4096, "available", null, 1, "restricted", true,
        "m0_d16_v1", new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31),
        new DateOnly(2026, 12, 31), new DateOnly(2027, 1, 1), [], [], true);
}
