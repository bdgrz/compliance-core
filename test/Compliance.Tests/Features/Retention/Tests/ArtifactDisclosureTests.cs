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

    static ArtifactDisclosure NewDisclosure() => new(TenantId, DisclosureId);

    static ArtifactDisclosureSourceSnapshot Source() => new(
        new ArtifactRetentionSource(TenantId, "evidence_artifact", ArtifactId, new string('a', 64)),
        12, 12, 4096, "available", "restricted", 3);
}
