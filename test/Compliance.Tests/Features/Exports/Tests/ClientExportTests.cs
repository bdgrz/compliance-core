using Cntryl.Portia;
using Cntryl.Portia.Testing;
using ClientExportAggregate = Bdgrz.Compliance.Features.Exports.Aggregates.ClientExport;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Exports.Aggregates;
using Bdgrz.Compliance.Features.Exports.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Exports.Tests;

public sealed class ClientExportTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ExportId = Uuid.CreateVersion4();
    static readonly Uuid ActorId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldReconcileIncludedAndWithheldRecordsToExactSourceSnapshotGivenRequest()
    {
        // Arrange
        var export = NewExport();
        var content = Content();

        // Act
        var requested = export.Request(content, ActorId, "Tenant owner", Now);

        // Assert
        Assert.Null(requested);
        Assert.Equal(2, export.Current!.Content.Records.Count);
        Assert.Equal("withheld_retention", export.Current.Content.Records.Single(item =>
            item.RecordId == WithheldRecordId).WithheldReasonCode);
        Assert.Equal(ContentHash, export.Current.Content.SourceSnapshotSha256);
        Assert.False(export.Current.IsComplete);
    }

    [Fact]
    public void ShouldRejectUnexplainedWithholdingAndDuplicateSourceRecordsGivenRequest()
    {
        // Arrange
        var export = NewExport();
        var content = Content();
        var unexplained = content with
        {
            Records = [content.Records[0], content.Records[1] with { WithheldReasonCode = null, WithheldReason = null }],
        };
        var duplicate = content with { Records = [content.Records[0], content.Records[0]] };

        // Act
        var missingReason = export.Request(unexplained, ActorId, "Tenant owner", Now);
        var duplicateSource = export.Request(duplicate, ActorId, "Tenant owner", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(missingReason).Code);
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(duplicateSource).Code);
        Assert.Equal(0, export.Revision);
    }

    [Fact]
    public void ShouldKeepInterruptedBuildIncompleteAndAllowExactRetryGivenExportRequest()
    {
        // Arrange
        var export = RequestedExport();

        // Act
        var failed = export.RecordBuildResult(export.Revision,
            new ClientExportBuildResultContent(Uuid.CreateVersion4(), 1, "failed", null, null,
                "archive_timeout", null, 0, 0, SourceSnapshotId, ContentHash),
            ActorId, "Export worker", Now.AddMinutes(1));
        var failedStatus = export.Current!.Status;
        var incompleteAfterFailure = export.Current.IsComplete;
        var successful = export.RecordBuildResult(export.Revision,
            new ClientExportBuildResultContent(Uuid.CreateVersion4(), 2, "built", Uuid.CreateVersion4(),
                Hash('a'), null, Uuid.CreateVersion4(), 1, 1, SourceSnapshotId, ContentHash),
            ActorId, "Export worker", Now.AddMinutes(2));

        // Assert
        Assert.Null(failed);
        Assert.Equal("build_failed", failedStatus);
        Assert.False(incompleteAfterFailure);
        Assert.Null(successful);
        Assert.Equal("built", export.Current.Status);
        Assert.False(export.Current.IsComplete);
        Assert.Equal(2, export.Current.BuildAttempts.Count);
        Assert.Equal(1, export.Current.BuildAttempts[^1].IncludedRecordCount);
        Assert.Equal(1, export.Current.BuildAttempts[^1].WithheldRecordCount);
    }

    [Fact]
    public void ShouldRequireAttestDeliveryDecisionBeforeFinalExportDeliveryGivenCollaborationBundle()
    {
        // Arrange
        var export = RequestedExport(Content("engagement_collaboration"));
        Assert.Null(export.RecordBuildResult(export.Revision,
            new ClientExportBuildResultContent(Uuid.CreateVersion4(), 1, "built", Uuid.CreateVersion4(),
                Hash('b'), null, Uuid.CreateVersion4(), 1, 1, SourceSnapshotId, ContentHash),
            ActorId, "Export worker", Now.AddMinutes(1)));

        // Act
        var premature = export.Deliver(export.Revision, "attest team", "delivery-1",
            ActorId, "Tenant owner", Now.AddMinutes(2));
        var decision = export.RecordAttestPackageDelivery(export.Revision, PackageId, "delivered",
            "Delivered the complete M0-D27 collaboration bundle to audit software.",
            "attest-team", "audit-system-receipt-88", ActorId, "Tenant owner", Now.AddMinutes(2));
        var delivered = export.Deliver(export.Revision, "attest team", "delivery-1",
            ActorId, "Tenant owner", Now.AddMinutes(3));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(premature).Code);
        Assert.Null(decision);
        Assert.Null(delivered);
        Assert.Equal("delivered", export.Current!.Status);
        Assert.True(export.Current.IsComplete);
        Assert.Equal("audit-system-receipt-88", export.Current.AttestPackageDeliveries.Single().DeliveryReference);
    }

    [Fact]
    public void ShouldAllowSuccessfulAttestDeliveryAfterRecordedInterruptionGivenCollaborationBundle()
    {
        // Arrange
        var export = RequestedExport(Content("engagement_collaboration"));
        Assert.Null(export.RecordBuildResult(export.Revision,
            new ClientExportBuildResultContent(Uuid.CreateVersion4(), 1, "built", Uuid.CreateVersion4(),
                Hash('d'), null, Uuid.CreateVersion4(), 1, 1, SourceSnapshotId, ContentHash),
            ActorId, "Export worker", Now.AddMinutes(1)));
        Assert.Null(export.RecordAttestPackageDelivery(export.Revision, PackageId, "not_delivered",
            "Audit software was unavailable.", "attest-team", null, ActorId, "Tenant owner", Now.AddMinutes(2)));

        // Act
        var premature = export.Deliver(export.Revision, "attest team", "delivery-2",
            ActorId, "Tenant owner", Now.AddMinutes(3));
        var retry = export.RecordAttestPackageDelivery(export.Revision, PackageId, "delivered",
            "Delivered after audit software recovered.", "attest-team", "audit-system-receipt-89",
            ActorId, "Tenant owner", Now.AddMinutes(4));
        var delivered = export.Deliver(export.Revision, "attest team", "delivery-2",
            ActorId, "Tenant owner", Now.AddMinutes(5));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(premature).Code);
        Assert.Null(retry);
        Assert.Null(delivered);
        Assert.Equal("delivered", export.Current!.Status);
        Assert.Equal(2, export.Current.AttestPackageDeliveries.Count);
        Assert.Equal("not_delivered", export.Current.AttestPackageDeliveries[0].Decision);
        Assert.Equal("delivered", export.Current.AttestPackageDeliveries[1].Decision);
        Assert.Equal("audit-system-receipt-89", export.Current.AttestPackageDeliveries[1].DeliveryReference);
    }

    [Fact]
    public void ShouldReplayBuildAndDeliveryHistoryGivenRetainedExportEvents()
    {
        // Arrange
        var export = RequestedExport();
        Assert.Null(export.RecordBuildResult(export.Revision,
            new ClientExportBuildResultContent(Uuid.CreateVersion4(), 1, "built", Uuid.CreateVersion4(),
                Hash('c'), null, Uuid.CreateVersion4(), 1, 1, SourceSnapshotId, ContentHash),
            ActorId, "Export worker", Now.AddMinutes(1)));
        Assert.Null(export.Deliver(export.Revision, "tenant owner", "download-42",
            ActorId, "Tenant owner", Now.AddMinutes(2)));
        var events = new AggregateScenario<ClientExportAggregate>(export).PendingEvents.ToArray();

        // Act
        var replay = new AggregateScenario<ClientExportAggregate>(NewExport()).Given(events).Aggregate;

        // Assert
        Assert.Equal("delivered", replay.Current!.Status);
        Assert.Equal(export.Current!.ContentSha256, replay.Current.ContentSha256);
        Assert.Equal("download-42", replay.Current.Delivery!.DeliveryReference);
        Assert.Equal(export.Current.BuildAttempts, replay.Current.BuildAttempts);
    }

    static readonly Uuid SourceSnapshotId = Uuid.CreateVersion4();
    static readonly Uuid PackageId = Uuid.CreateVersion4();
    static readonly Uuid WithheldRecordId = Uuid.CreateVersion4();
    static readonly string ContentHash = Hash('f');

    static ClientExportAggregate NewExport() => new(TenantId, ExportId);

    static ClientExportAggregate RequestedExport(ClientExportContent? content = null)
    {
        var export = NewExport();
        Assert.Null(export.Request(content ?? Content(), ActorId, "Tenant owner", Now));
        return export;
    }

    static ClientExportContent Content(string scope = "organization") => new(scope, "zip",
        "offboarding-export-2026", SourceSnapshotId, ContentHash, 2,
        [new("tenant_record", Uuid.CreateVersion4(), 2, Hash('d'), "included", null, null, []),
         new("evidence_artifact", WithheldRecordId, 1, Hash('e'), "withheld",
             "withheld_retention", "Held under engagement preservation policy.", [])],
        scope == "engagement_collaboration"
            ? [new(Uuid.CreateVersion4(), PackageId, Hash('1'), 12)] : []);

    static string Hash(char value) => new(value, 64);
}
