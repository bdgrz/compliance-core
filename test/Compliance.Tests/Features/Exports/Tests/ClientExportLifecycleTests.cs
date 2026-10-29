using Cntryl.Portia;
using Cntryl.Portia.Testing;
using ClientExportAggregate = Bdgrz.Compliance.Features.Exports.Aggregates.ClientExport;
using Bdgrz.Compliance.Features.Exports.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
namespace Bdgrz.Compliance.Tests.Features.Exports.Tests;

public sealed class ClientExportLifecycleTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ExportId = Uuid.CreateVersion4();
    static readonly Uuid ActorId = Uuid.CreateVersion4();
    static readonly Uuid SourceSnapshotId = Uuid.CreateVersion4();
    static readonly Uuid PackageId = Uuid.CreateVersion4();
    static readonly Uuid IncludedRecordId = Uuid.CreateVersion4();
    static readonly Uuid WithheldRecordId = Uuid.CreateVersion4();
    static readonly Uuid ManifestPackageEngagementId = Uuid.CreateVersion4();
    static readonly string ContentHash = new('f', 64);
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldDeriveIdentityFromExportIdGivenTenantAndExport()
    {
        // Arrange
        var first = new ClientExportAggregate(TenantId, ExportId);

        // Act
        var second = new ClientExportAggregate(TenantId, ExportId);

        // Assert
        Assert.Equal(ExportId, first.Id);
        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public void ShouldAcknowledgeExactRequestRetryWithoutNewFactGivenLaterBuildFact()
    {
        // Arrange
        var export = Built(Content());

        // Act
        var retry = export.Request(Content(), ActorId, "Tenant owner", Now);

        // Assert
        Assert.Null(retry);
        Assert.Equal(2, Pending(export));
        Assert.Equal(2, export.Revision);
    }

    [Fact]
    public void ShouldRefuseChangedRequestWithoutNewFactGivenRequestedExport()
    {
        // Arrange
        var export = Requested(Content());

        // Act
        var changed = export.Request(Content("engagement_collaboration"), ActorId, "Tenant owner", Now);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(changed).Code);
        Assert.Equal(1, export.Revision);
        Assert.Equal(1, Pending(export));
    }

    [Fact]
    public void ShouldRefuseBuildResultGivenExportNotRequested()
    {
        // Arrange
        var export = new ClientExportAggregate(TenantId, ExportId);

        // Act
        var failure = export.RecordBuildResult(0, Built(1, Uuid.CreateVersion4()), ActorId, "Export worker", Now);

        // Assert
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(0, Pending(export));
    }

    [Fact]
    public void ShouldRefuseBuildResultWithoutNewFactGivenStaleExpectedRevision()
    {
        // Arrange
        var export = Requested(Content());

        // Act
        var failure = export.RecordBuildResult(0, Built(1, Uuid.CreateVersion4()), ActorId, "Export worker", Now.AddMinutes(1));

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(1, export.Revision);
        Assert.Equal(1, Pending(export));
    }

    [Fact]
    public void ShouldAcknowledgeBuildAttemptRetryWithoutNewFactGivenLaterAttempt()
    {
        // Arrange
        var export = Requested(Content());
        var failedAttempt = Uuid.CreateVersion4();
        var failed = Failed(1, failedAttempt, "archive_timeout");
        Assert.Null(export.RecordBuildResult(export.Revision, failed, ActorId, "Export worker", Now.AddMinutes(1)));
        Assert.Null(export.RecordBuildResult(export.Revision, Built(2, Uuid.CreateVersion4()), ActorId, "Export worker", Now.AddMinutes(2)));

        // Act
        var retry = export.RecordBuildResult(1, Failed(1, failedAttempt, "Archive_Timeout"), ActorId, "Export worker", Now.AddMinutes(1));

        // Assert
        Assert.Null(retry);
        Assert.Equal(3, Pending(export));
        Assert.Equal(3, export.Revision);
    }

    [Fact]
    public void ShouldRefuseBuildAttemptIdReuseGivenChangedAttemptNumber()
    {
        // Arrange
        var export = Requested(Content());
        var attemptId = Uuid.CreateVersion4();
        Assert.Null(export.RecordBuildResult(export.Revision, Failed(1, attemptId, "archive_timeout"),
            ActorId, "Export worker", Now.AddMinutes(1)));

        // Act
        var reused = export.RecordBuildResult(export.Revision, Failed(2, attemptId, "archive_timeout"),
            ActorId, "Export worker", Now.AddMinutes(2));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(reused).Code);
        Assert.Equal(2, export.Revision);
        Assert.Equal(2, Pending(export));
    }

    [Fact]
    public void ShouldRefuseBuildResultWithoutNewFactGivenDeliveredExport()
    {
        // Arrange
        var export = Built(Content());
        Assert.Null(export.Deliver(export.Revision, "tenant owner", "download-1", ActorId, "Tenant owner", Now.AddMinutes(2)));

        // Act
        var late = export.RecordBuildResult(export.Revision, Built(2, Uuid.CreateVersion4()), ActorId,
            "Export worker", Now.AddMinutes(3));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(late).Code);
        Assert.Equal(3, Pending(export));
    }

    [Fact]
    public void ShouldRefuseDeliveryWithoutNewFactGivenOnlyFailedBuild()
    {
        // Arrange
        var export = Requested(Content());
        Assert.Null(export.RecordBuildResult(export.Revision, Failed(1, Uuid.CreateVersion4(), "archive_timeout"),
            ActorId, "Export worker", Now.AddMinutes(1)));

        // Act
        var failure = export.Deliver(export.Revision, "tenant owner", "download-1", ActorId, "Tenant owner", Now.AddMinutes(2));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.False(export.Current!.IsComplete);
        Assert.Equal(2, Pending(export));
    }

    [Fact]
    public void ShouldRefuseDeliveryWithoutNewFactGivenStaleExpectedRevision()
    {
        // Arrange
        var export = Built(Content());

        // Act
        var failure = export.Deliver(1, "tenant owner", "download-1", ActorId, "Tenant owner", Now.AddMinutes(2));

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(2, Pending(export));
    }

    [Fact]
    public void ShouldAcknowledgeExactDeliveryRetryAndRefuseChangedReceiptGivenDeliveredExport()
    {
        // Arrange
        var export = Built(Content());
        Assert.Null(export.Deliver(export.Revision, "tenant owner", "download-1", ActorId, "Tenant owner", Now.AddMinutes(2)));

        // Act
        var retry = export.Deliver(2, "tenant owner", "download-1", ActorId, "Tenant owner", Now.AddMinutes(2));
        var changed = export.Deliver(3, "tenant owner", "download-2", ActorId, "Tenant owner", Now.AddMinutes(3));

        // Assert
        Assert.Null(retry);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(changed).Code);
        Assert.Equal(3, Pending(export));
        Assert.Equal("download-1", export.Current!.Delivery!.DeliveryReference);
    }

    [Fact]
    public void ShouldAcknowledgeEarlierAttestDecisionRetryWithoutNewFactGivenLaterDeliveredDecision()
    {
        // Arrange
        var export = Built(Content("engagement_collaboration"));
        Assert.Null(export.RecordAttestPackageDelivery(export.Revision, PackageId, "not_delivered",
            "Audit software was unavailable.", "attest-team", null, ActorId, "Tenant owner", Now.AddMinutes(2)));
        Assert.Null(export.RecordAttestPackageDelivery(export.Revision, PackageId, "delivered",
            "Delivered after recovery.", "attest-team", "receipt-1", ActorId, "Tenant owner", Now.AddMinutes(3)));

        // Act
        var retry = export.RecordAttestPackageDelivery(2, PackageId, "not_delivered",
            "Audit software was unavailable.", "attest-team", null, ActorId, "Tenant owner", Now.AddMinutes(2));

        // Assert
        Assert.Null(retry);
        Assert.Equal(4, Pending(export));
        Assert.Equal(2, export.Current!.AttestPackageDeliveries.Count);
    }

    [Fact]
    public void ShouldRefuseChangedAttestDecisionWithoutNewFactGivenDeliveredPackage()
    {
        // Arrange
        var export = Built(Content("engagement_collaboration"));
        Assert.Null(export.RecordAttestPackageDelivery(export.Revision, PackageId, "delivered",
            "Delivered.", "attest-team", "receipt-1", ActorId, "Tenant owner", Now.AddMinutes(2)));

        // Act
        var changed = export.RecordAttestPackageDelivery(export.Revision, PackageId, "delivered",
            "Delivered.", "attest-team", "receipt-2", ActorId, "Tenant owner", Now.AddMinutes(3));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(changed).Code);
        Assert.Equal(3, Pending(export));
    }

    [Fact]
    public void ShouldRefuseAttestDecisionWithoutNewFactGivenPackageNotRequiredByExport()
    {
        // Arrange
        var export = Built(Content("engagement_collaboration"));

        // Act
        var failure = export.RecordAttestPackageDelivery(export.Revision, Uuid.CreateVersion4(), "delivered",
            "Delivered.", "attest-team", "receipt-1", ActorId, "Tenant owner", Now.AddMinutes(2));

        // Assert
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(2, Pending(export));
    }

    [Fact]
    public void ShouldRefuseDeliveredAttestDecisionWithoutNewFactGivenMissingReceipt()
    {
        // Arrange
        var export = Built(Content("engagement_collaboration"));

        // Act
        var failure = export.RecordAttestPackageDelivery(export.Revision, PackageId, "delivered",
            "Delivered.", null, null, ActorId, "Tenant owner", Now.AddMinutes(2));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Empty(export.Current!.AttestPackageDeliveries);
        Assert.Equal(2, Pending(export));
    }

    [Fact]
    public void ShouldRestoreStateAndAcceptDeliveryGivenReplayedBuiltHistory()
    {
        // Arrange
        var seed = Built(Content());
        var events = new AggregateScenario<ClientExportAggregate>(seed).PendingEvents.ToArray();
        var replay = new AggregateScenario<ClientExportAggregate>(new ClientExportAggregate(TenantId, ExportId))
            .Given(events).Aggregate;
        var replayedStatus = replay.Current!.Status;
        var replayedRevision = replay.Revision;

        // Act
        var retry = replay.Request(Content(), ActorId, "Tenant owner", Now);
        var delivered = replay.Deliver(replay.Revision, "tenant owner", "download-1", ActorId, "Tenant owner", Now.AddMinutes(2));

        // Assert
        Assert.Equal("built", replayedStatus);
        Assert.Equal(2, replayedRevision);
        Assert.Null(retry);
        Assert.Null(delivered);
        Assert.True(replay.Current!.IsComplete);
    }

    static int Pending(ClientExportAggregate export) => new AggregateScenario<ClientExportAggregate>(export).PendingEvents.Count;

    static ClientExportAggregate Requested(ClientExportContent content)
    {
        var export = new ClientExportAggregate(TenantId, ExportId);
        Assert.Null(export.Request(content, ActorId, "Tenant owner", Now));
        return export;
    }

    static ClientExportAggregate Built(ClientExportContent content)
    {
        var export = Requested(content);
        Assert.Null(export.RecordBuildResult(export.Revision, Built(1, Uuid.CreateVersion4()),
            ActorId, "Export worker", Now.AddMinutes(1)));
        return export;
    }

    static ClientExportBuildResultContent Built(int attemptNumber, Uuid attemptId) =>
        new(attemptId, attemptNumber, "built", Uuid.CreateVersion4(), new string('a', 64), null,
            Uuid.CreateVersion4(), 1, 1, SourceSnapshotId, ContentHash);

    static ClientExportBuildResultContent Failed(int attemptNumber, Uuid attemptId, string code) =>
        new(attemptId, attemptNumber, "failed", null, null, code, null, 0, 0, SourceSnapshotId, ContentHash);

    static ClientExportContent Content(string scope = "organization") => new(scope, "zip",
        "offboarding-export-2026", SourceSnapshotId, ContentHash, 2,
        [new("tenant_record", IncludedRecordId, 2, new string('d', 64), "included", null, null, []),
         new("evidence_artifact", WithheldRecordId, 1, new string('e', 64), "withheld",
             "withheld_retention", "Held under engagement preservation policy.", [])],
        scope == "engagement_collaboration"
            ? [new(ManifestPackageEngagementId, PackageId, new string('1', 64), 12)] : []);
}
