using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Retention.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Retention.Tests;

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

    [Fact]
    public void ShouldAcknowledgeRetryOfCommittedResultGivenLaterAttemptRequested()
    {
        // Arrange
        var disposition = NewDisposition();
        Assert.True(disposition.Approve(Eligible(), 0, "Retention elapsed", Admin, Now).IsSuccess);
        Assert.True(disposition.RequestExecution(1, Eligible(), Worker, Now.AddHours(1)).IsSuccess);
        Assert.True(disposition.RecordExecutionResult(2, "failed", "storage_timeout", Worker,
            Now.AddHours(2)).IsSuccess);
        Assert.True(disposition.RequestExecution(3, Eligible(), Worker, Now.AddHours(3)).IsSuccess);
        var revision = disposition.Revision;

        // Act
        var retry = disposition.RecordExecutionResult(2, "failed", "storage_timeout", Worker,
            Now.AddHours(2));
        var changed = disposition.RecordExecutionResult(2, "failed", "other_code", Worker,
            Now.AddHours(2));

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Equal(revision, disposition.Revision);
        Assert.Equal(4, revision);
    }

    [Fact]
    public void ShouldAcknowledgeRetryOfCommittedRequestGivenLaterResultRecorded()
    {
        // Arrange
        var disposition = NewDisposition();
        Assert.True(disposition.Approve(Eligible(), 0, "Retention elapsed", Admin, Now).IsSuccess);
        Assert.True(disposition.RequestExecution(1, Eligible(), Worker, Now.AddHours(1)).IsSuccess);
        Assert.True(disposition.RecordExecutionResult(2, "failed", "storage_timeout", Worker,
            Now.AddHours(2)).IsSuccess);

        // Act
        var retry = disposition.RequestExecution(1, Eligible(), Worker, Now.AddHours(1));
        var changed = disposition.RequestExecution(1, Eligible(), Worker, Now.AddHours(5));

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Equal(3, disposition.Revision);
    }

    [Fact]
    public void ShouldAcknowledgeExactRetryAndRefuseChangedIntentGivenApprovedDisposition()
    {
        // Arrange
        var disposition = NewDisposition();
        Assert.True(disposition.Approve(Eligible(), 0, "Retention elapsed", Admin, Now).IsSuccess);

        // Act
        var retry = disposition.Approve(Eligible(), 0, "Retention elapsed", Admin, Now);
        var changed = disposition.Approve(Eligible(), 0, "Different rationale", Admin, Now);
        var systemActor = NewDisposition().Approve(Eligible(), 0, "Retention elapsed", Worker, Now);
        var stale = NewDisposition().Approve(Eligible(), 5, "Retention elapsed", Admin, Now);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, systemActor.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.Equal(1, disposition.Revision);
    }

    [Fact]
    public void ShouldRefuseSecondAttemptAndResultWithoutOutstandingAttemptGivenExecutionState()
    {
        // Arrange
        var disposition = NewDisposition();
        Assert.True(disposition.Approve(Eligible(), 0, "Retention elapsed", Admin, Now).IsSuccess);

        // Act
        var earlyResult = disposition.RecordExecutionResult(1, "disposed", null, Worker, Now.AddHours(1));
        var requested = disposition.RequestExecution(1, Eligible(), Worker, Now.AddHours(1));
        var second = disposition.RequestExecution(2, Eligible(), Worker, Now.AddHours(2));
        var staleResult = disposition.RecordExecutionResult(1, "disposed", null, Worker, Now.AddHours(3));
        var invalidOutcome = disposition.RecordExecutionResult(2, "disposed", "code", Worker,
            Now.AddHours(3));
        var disposed = disposition.RecordExecutionResult(2, "disposed", null, Worker, Now.AddHours(4));
        var afterTerminal = disposition.RequestExecution(3, Eligible(), Worker, Now.AddHours(5));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, earlyResult.Error!.Kind);
        Assert.True(requested.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, second.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, staleResult.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, invalidOutcome.Error!.Kind);
        Assert.True(disposed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, afterTerminal.Error!.Kind);
        Assert.Equal(3, disposition.Revision);
        Assert.Single(disposition.Current!.Attempts);
    }

    [Fact]
    public void ShouldRefuseSeventeenthAttemptGivenSixteenFailedAttempts()
    {
        // Arrange
        var disposition = NewDisposition();
        Assert.True(disposition.Approve(Eligible(), 0, "Retention elapsed", Admin, Now).IsSuccess);
        for (var attempt = 1; attempt <= 16; attempt++)
        {
            var requestedAt = Now.AddHours(attempt * 2);
            Assert.True(disposition.RequestExecution(disposition.Revision, Eligible(), Worker,
                requestedAt).IsSuccess);
            Assert.True(disposition.RecordExecutionResult(disposition.Revision, "failed",
                "storage_timeout", Worker, requestedAt.AddHours(1)).IsSuccess);
        }

        var revision = disposition.Revision;

        // Act
        var seventeenth = disposition.RequestExecution(revision, Eligible(), Worker, Now.AddHours(100));

        // Assert
        Assert.False(seventeenth.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, seventeenth.Error!.Kind);
        Assert.Equal(revision, disposition.Revision);
        Assert.Equal(16, disposition.Current!.Attempts.Count);
    }

    static ArtifactDisposition NewDisposition() => new(TenantId, DispositionId);

    static ArtifactDispositionEligibilitySnapshot Eligible() => new(
        new ArtifactRetentionSource(TenantId, "evidence_artifact", ArtifactId,
            new string('a', 64)), 12, 4096, "available", null, 1, "restricted", true,
        "m0_d16_v1", new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31),
        new DateOnly(2026, 12, 31), new DateOnly(2027, 1, 1), [], [], true);
}
