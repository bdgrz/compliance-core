using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Retention.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Testing;
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
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Retention.Tests;

public sealed class ArtifactDisclosureTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ArtifactId = Uuid.CreateVersion4();
    static readonly Uuid DisclosureId = Uuid.CreateVersion4();
    static readonly ActorReference Approver = ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance lead");
    static readonly ActorReference Dispatcher = ActorReference.ForMember(Uuid.CreateVersion4(), "Records owner");
    static readonly DateTimeOffset Now = new(2026, 10, 17, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRequireAvailableClassifiedArtifactGivenApproval()
    {
        // Arrange
        var disclosure = NewDisclosure();

        // Act
        var unavailable = disclosure.Approve(Source() with { ArtifactState = "quarantined" },
            "External auditor", "Type I package", "Reviewed", Approver, Now);
        var unclassified = disclosure.Approve(Source() with { HandlingPolicyRevision = 0 },
            "External auditor", "Type I package", "Reviewed", Approver, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(unavailable.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(unclassified.Error).Kind);
        Assert.Null(disclosure.Current);
    }

    [Fact]
    public void ShouldFenceDeliveryToApprovedPolicyVersionGivenPolicyChange()
    {
        // Arrange
        var disclosure = NewDisclosure();
        Assert.True(disclosure.Approve(Source(), "External auditor", "Type I package",
            "Only approved evidence", Approver, Now).IsSuccess);

        // Act
        var stalePolicy = disclosure.Deliver(Source() with { HandlingPolicyRevision = 2 }, 1,
            "delivery-001", Dispatcher, Now.AddHours(1));
        var delivered = disclosure.Deliver(Source(), 1, "delivery-001", Dispatcher,
            Now.AddHours(2));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(stalePolicy.Error).Kind);
        Assert.True(delivered.IsSuccess);
        Assert.Equal("delivered", disclosure.Current!.Status);
        Assert.Equal("delivery-001", disclosure.Current.Delivery!.DeliveryReference);
        Assert.False(disclosure.Current.DownloadedCopiesRecallable);
    }

    [Fact]
    public void ShouldPreserveDeliveryAndRecordNonrecallableRevocationGivenRevoke()
    {
        // Arrange
        var disclosure = NewDisclosure();
        Assert.True(disclosure.Approve(Source(), "External auditor", "Type I package",
            "Approved disclosure", Approver, Now).IsSuccess);
        Assert.True(disclosure.Deliver(Source(), 1, "delivery-002", Dispatcher,
            Now.AddHours(1)).IsSuccess);

        // Act
        var stale = disclosure.Revoke(1, "Access no longer needed", Approver, Now.AddHours(2));
        var revoked = disclosure.Revoke(2, "Access no longer needed", Approver, Now.AddHours(2));
        var events = new AggregateScenario<ArtifactDisclosure>(disclosure).PendingEvents.ToArray();
        var replay = new AggregateScenario<ArtifactDisclosure>(NewDisclosure()).Given(events).Aggregate;

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(stale.Error).Kind);
        Assert.True(revoked.IsSuccess);
        Assert.Equal("revoked", disclosure.Current!.Status);
        Assert.Equal("delivery-002", disclosure.Current.Delivery!.DeliveryReference);
        Assert.False(disclosure.Current.DownloadedCopiesRecallable);
        Assert.Equal(disclosure.Current.Status, replay.Current!.Status);
        Assert.Equal(disclosure.Current.Delivery, replay.Current.Delivery);
    }

    [Fact]
    public void ShouldAllowOnlyExactApprovalRetryGivenDisclosureIdentity()
    {
        // Arrange
        var disclosure = NewDisclosure();
        Assert.True(disclosure.Approve(Source(), "External auditor", "Type I package",
            "Approved disclosure", Approver, Now).IsSuccess);

        // Act
        var exactRetry = disclosure.Approve(Source(), "External auditor", "Type I package",
            "Approved disclosure", Approver, Now);
        var changedRetry = disclosure.Approve(Source(), "Other recipient", "Type I package",
            "Approved disclosure", Approver, Now);

        // Assert
        Assert.True(exactRetry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changedRetry.Error).Kind);
        Assert.Equal("External auditor", disclosure.Current!.Recipient);
        Assert.Single(new AggregateScenario<ArtifactDisclosure>(disclosure).PendingEvents);
    }

    [Fact]
    public void ShouldReturnOriginalApprovalGivenExactRetryAfterDeliveryAndSourceDrift()
    {
        // Arrange
        var disclosure = NewDisclosure();
        Assert.True(Approve(disclosure).IsSuccess);
        Assert.True(disclosure.Deliver(Source(), 1, "delivery-010", Dispatcher, Now.AddHours(1)).IsSuccess);

        // Act
        var retry = disclosure.Approve(Source() with { ArtifactState = "quarantined", HandlingPolicyRevision = 9 },
            "External auditor", "Type I package", "Approved disclosure", Approver, Now);
        var otherContent = new ArtifactRetentionSource(TenantId, "evidence_artifact", ArtifactId, new string('b', 64));
        var changedArtifact = disclosure.Approve(Source() with { Source = otherContent },
            "External auditor", "Type I package", "Approved disclosure", Approver, Now);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Kind(changedArtifact));
        Assert.Equal("delivered", disclosure.Current!.Status);
        Assert.Equal(2, PendingCount(disclosure));
    }

    [Fact]
    public void ShouldAppendNothingGivenInvalidApprovalOrMissingDisclosure()
    {
        // Arrange
        var disclosure = NewDisclosure();
        var oversized = new string('r', 501);

        // Act
        var blankRecipient = disclosure.Approve(Source(), " ", "Type I package", "Reviewed", Approver, Now);
        var oversizedRecipient = disclosure.Approve(Source(), oversized, "Type I package", "Reviewed", Approver, Now);
        var noTimestamp = disclosure.Approve(Source(), "External auditor", "Type I package", "Reviewed", Approver,
            default);
        var missingDelivery = disclosure.Deliver(Source(), 0, "delivery-011", Dispatcher, Now);
        var missingRevoke = disclosure.Revoke(0, "Not needed", Approver, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Kind(blankRecipient));
        Assert.Equal(RequestErrorKind.Validation, Kind(oversizedRecipient));
        Assert.Equal(RequestErrorKind.Validation, Kind(noTimestamp));
        Assert.Equal(RequestErrorKind.NotFound, Kind(missingDelivery));
        Assert.Equal(RequestErrorKind.NotFound, Kind(missingRevoke));
        Assert.Null(disclosure.Current);
        Assert.Equal(0, PendingCount(disclosure));
    }

    [Fact]
    public void ShouldRefuseDeliveryAndAppendNothingGivenStaleRevisionOrInvalidReceipt()
    {
        // Arrange
        var disclosure = NewDisclosure();
        Assert.True(Approve(disclosure).IsSuccess);

        // Act
        var stale = disclosure.Deliver(Source(), 0, "delivery-012", Dispatcher, Now.AddHours(1));
        var blankReference = disclosure.Deliver(Source(), 1, " ", Dispatcher, Now.AddHours(1));
        var unavailable = disclosure.Deliver(Source() with { ArtifactState = "quarantined" }, 1, "delivery-012",
            Dispatcher, Now.AddHours(1));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Kind(stale));
        Assert.Equal(RequestErrorKind.Validation, Kind(blankReference));
        Assert.Equal(RequestErrorKind.Conflict, Kind(unavailable));
        Assert.Equal("approved", disclosure.Current!.Status);
        Assert.Equal(1, PendingCount(disclosure));
    }

    [Fact]
    public void ShouldReturnOriginalReceiptGivenExactDeliveryRetryAfterRevocationAndRefuseChangedReceipt()
    {
        // Arrange
        var disclosure = NewDisclosure();
        Assert.True(Approve(disclosure).IsSuccess);
        Assert.True(disclosure.Deliver(Source(), 1, "delivery-013", Dispatcher, Now.AddHours(1)).IsSuccess);
        Assert.True(disclosure.Revoke(2, "Access no longer needed", Approver, Now.AddHours(2)).IsSuccess);

        // Act
        var retry = disclosure.Deliver(Source() with { HandlingPolicyRevision = 7, Position = 40 }, 2,
            "delivery-013", Dispatcher, Now.AddHours(1));
        var staleRetry = disclosure.Deliver(Source(), 1, "delivery-013", Dispatcher, Now.AddHours(1));
        var changedReference = disclosure.Deliver(Source(), 2, "delivery-other", Dispatcher, Now.AddHours(1));
        var changedTime = disclosure.Deliver(Source(), 2, "delivery-013", Dispatcher, Now.AddHours(5));
        var missingSource = disclosure.Deliver(null!, 2, "delivery-013", Dispatcher, Now.AddHours(1));

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.True(staleRetry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Kind(changedReference));
        Assert.Equal(RequestErrorKind.Conflict, Kind(changedTime));
        Assert.Equal(RequestErrorKind.Conflict, Kind(missingSource));
        Assert.Equal("revoked", disclosure.Current!.Status);
        Assert.Equal(3, PendingCount(disclosure));
    }

    [Fact]
    public void ShouldRefuseDeliveryGivenRevokedUndeliveredDisclosure()
    {
        // Arrange
        var disclosure = NewDisclosure();
        Assert.True(Approve(disclosure).IsSuccess);
        Assert.True(disclosure.Revoke(1, "Recipient withdrawn", Approver, Now.AddHours(1)).IsSuccess);

        // Act
        var delivery = disclosure.Deliver(Source(), 2, "delivery-014", Dispatcher, Now.AddHours(2));
        var secondRevoke = disclosure.Revoke(2, "Different reason", Approver, Now.AddHours(3));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Kind(delivery));
        Assert.Equal(RequestErrorKind.Conflict, Kind(secondRevoke));
        Assert.Equal("revoked", disclosure.Current!.Status);
        Assert.Null(disclosure.Current.Delivery);
        Assert.Equal(2, PendingCount(disclosure));
    }

    [Fact]
    public void ShouldAcceptExactRevokeRetryAndRefuseChangedDecisionGivenRevokedDisclosure()
    {
        // Arrange
        var disclosure = NewDisclosure();
        Assert.True(Approve(disclosure).IsSuccess);
        Assert.True(disclosure.Revoke(1, "Recipient withdrawn", Approver, Now.AddHours(1)).IsSuccess);

        // Act
        var retry = disclosure.Revoke(1, "  Recipient withdrawn ", Approver, Now.AddHours(1));
        var changedRationale = disclosure.Revoke(1, "Other reason", Approver, Now.AddHours(1));
        var changedTime = disclosure.Revoke(1, "Recipient withdrawn", Approver, Now.AddHours(4));
        var invalid = disclosure.Revoke(1, " ", Approver, Now.AddHours(1));

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Kind(changedRationale));
        Assert.Equal(RequestErrorKind.Conflict, Kind(changedTime));
        Assert.Equal(RequestErrorKind.Validation, Kind(invalid));
        Assert.Equal(2, PendingCount(disclosure));
    }

    [Fact]
    public void ShouldRestoreStateAndAcceptRetriesGivenValidHistoryReplay()
    {
        // Arrange
        var seed = NewDisclosure();
        Assert.True(Approve(seed).IsSuccess);
        Assert.True(seed.Deliver(Source(), 1, "delivery-015", Dispatcher, Now.AddHours(1)).IsSuccess);
        Assert.True(seed.Revoke(2, "Access no longer needed", Approver, Now.AddHours(2)).IsSuccess);
        var history = NativeFacts.Raised(seed);

        // Act
        var restored = NativeFacts.Replay(NewDisclosure(), history);
        var status = restored.Current!.Status;
        var revision = restored.Revision;
        var delivery = restored.Current.Delivery;
        var approveRetry = Approve(restored);
        var deliverRetry = restored.Deliver(Source(), 2, "delivery-015", Dispatcher, Now.AddHours(1));
        var revokeRetry = restored.Revoke(2, "Access no longer needed", Approver, Now.AddHours(2));
        var changedRevoke = restored.Revoke(3, "Other reason", Approver, Now.AddHours(2));

        // Assert
        Assert.Equal(3, history.Length);
        Assert.Equal("revoked", status);
        Assert.Equal(3, revision);
        Assert.Equal("delivery-015", delivery!.DeliveryReference);
        Assert.True(approveRetry.IsSuccess);
        Assert.True(deliverRetry.IsSuccess);
        Assert.True(revokeRetry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Kind(changedRevoke));
        Assert.Equal(0, PendingCount(restored));
    }

    static Result Approve(ArtifactDisclosure disclosure) => disclosure.Approve(Source(), "External auditor",
        "Type I package", "Approved disclosure", Approver, Now);

    static RequestErrorKind Kind(Result result) => Assert.IsType<RequestError>(result.Error).Kind;

    static int PendingCount(ArtifactDisclosure disclosure) =>
        new AggregateScenario<ArtifactDisclosure>(disclosure).PendingEvents.Count;

    static ArtifactDisclosure NewDisclosure() => new(TenantId, DisclosureId);

    static ArtifactDisclosureSourceSnapshot Source() => new(
        new ArtifactRetentionSource(TenantId, "evidence_artifact", ArtifactId, new string('a', 64)),
        12, 12, 4096, "available", "restricted", 3);
}
