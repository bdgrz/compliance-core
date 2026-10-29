using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
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
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Handlers;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class TenantMemberInvitedSerializationTests
{
    static readonly DateTimeOffset At = TenantInvitationFacts.At;

    [Fact]
    public void ShouldPreserveGuestScopeReferenceAndInvitationAttributionGivenEventRoundTrip()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var invitationId = Uuid.CreateVersion4();
        var invitation = new TenantInvitation(tenantId, invitationId);
        var invitedBy = Uuid.CreateVersion4();
        var scope = TenantInvitationFacts.Scope(TenantInvitationAdmission.GuestScopeSchema);
        var original = new TenantMemberInvited(tenantId, invitationId, "guest@example.com", "guest",
            false, new string('A', 64), At.AddDays(7), invitedBy, null, "current", scope, new DateOnly(2026, 11, 30), At,
            "Org Admin", 3, TenantInvitationFacts.Provenance(invitation, invitationId), TenantInvitationFacts.Response());
        var json = JsonSerializer.Serialize(original, ComplianceCoreJsonContext.Default.TenantMemberInvited);

        // Act
        var replayed = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.TenantMemberInvited);

        // Assert
        Assert.NotNull(replayed);
        Assert.Equal(invitationId, replayed.InvitationId);
        Assert.Equal("guest", replayed.Affiliation);
        Assert.Equal(scope, replayed.GuestProgramIdsManifest);
        Assert.Equal(new DateOnly(2026, 11, 30), replayed.GuestExpiresOn);
        Assert.Equal(At, replayed.InvitedAt);
        Assert.Equal(invitedBy, replayed.InvitedBy);
        Assert.Equal("Org Admin", replayed.InvitedByDisplay);
        Assert.Equal(3, replayed.MailboxGeneration);
        Assert.Equal(original.Operation, replayed.Operation);
        Assert.Equal(original.FirstResponse, replayed.FirstResponse);
    }

    [Fact]
    public void ShouldPreserveGuestTermsGivenAcceptanceEventRoundTrip()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var invitationId = Uuid.CreateVersion4();
        var invitation = new TenantInvitation(tenantId, invitationId);
        var scope = TenantInvitationFacts.Scope(AcceptTenantInvitationHandler.GuestScopeSchema);
        var original = new TenantInvitationAccepted(tenantId, invitationId, Uuid.CreateVersion4(),
            "guest@example.com", "guest", false, null, scope, new DateOnly(2026, 11, 30), At, Uuid.CreateVersion4(),
            "Org Admin", TenantInvitationFacts.Provenance(invitation, TenantInvitationIds.AcceptanceOf(invitationId),
                "AcceptTenantInvitation"), TenantInvitationFacts.Response());
        var json = JsonSerializer.Serialize(original, ComplianceCoreJsonContext.Default.TenantInvitationAccepted);

        // Act
        var replayed = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.TenantInvitationAccepted);

        // Assert
        Assert.NotNull(replayed);
        Assert.Equal(original.InvitationId, replayed.InvitationId);
        Assert.Equal("guest", replayed.Affiliation);
        Assert.Equal(scope, replayed.GuestProgramIdsManifest);
        Assert.Equal(new DateOnly(2026, 11, 30), replayed.GuestExpiresOn);
        Assert.Equal(At, replayed.AcceptedAt);
        Assert.Equal(original.InvitedBy, replayed.InvitedBy);
        Assert.Equal("Org Admin", replayed.InvitedByDisplay);
        Assert.Equal(original.Operation, replayed.Operation);
    }

    [Fact]
    public void ShouldRoundTripMailboxFactsGivenClaimAndRelease()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var invitationId = Uuid.CreateVersion4();
        var claimed = new TenantInvitationMailboxClaimed(tenantId, "member@example.com", invitationId, 2, At);
        var released = new TenantInvitationMailboxReleased(tenantId, "member@example.com", invitationId, 2,
            TenantInvitationMailboxClaim.AcceptedReason, At.AddDays(1));

        // Act
        var claimedBack = JsonSerializer.Deserialize(JsonSerializer.Serialize(claimed,
            ComplianceCoreJsonContext.Default.TenantInvitationMailboxClaimed),
            ComplianceCoreJsonContext.Default.TenantInvitationMailboxClaimed);
        var releasedBack = JsonSerializer.Deserialize(JsonSerializer.Serialize(released,
            ComplianceCoreJsonContext.Default.TenantInvitationMailboxReleased),
            ComplianceCoreJsonContext.Default.TenantInvitationMailboxReleased);

        // Assert
        Assert.Equal(claimed, claimedBack);
        Assert.Equal(released, releasedBack);
    }

    [Fact]
    public void ShouldRoundTripDeliveryRecordGivenAttemptResults()
    {
        // Arrange
        var invitationId = Uuid.CreateVersion4();
        var attemptId = TenantInvitationDeliveryRecord.AttemptIdFor(invitationId);
        var record = TenantInvitationDeliveryRecord.Start(Uuid.CreateVersion4(), invitationId)
            .Begin(attemptId, At).WithResult(attemptId, TenantInvitationDeliveryAttempt.Rejected, "delivery_failed", At) with
        {
            Status = TenantInvitationDeliveryRecord.Failed,
            Version = 3,
        };

        // Act
        var replayed = JsonSerializer.Deserialize(JsonSerializer.Serialize(record,
            ComplianceCoreJsonContext.Default.TenantInvitationDeliveryRecord),
            ComplianceCoreJsonContext.Default.TenantInvitationDeliveryRecord);

        // Assert
        Assert.NotNull(replayed);
        Assert.Equal(record.Status, replayed.Status);
        Assert.Equal(record.Version, replayed.Version);
        Assert.Equal(record.Attempts, replayed.Attempts);
    }

    [Fact]
    public void ShouldKeepAcceptanceIdDistinctFromInvitationIdGivenDerivation()
    {
        // Arrange
        var invitationId = Uuid.CreateVersion4();

        // Act
        var acceptance = TenantInvitationIds.AcceptanceOf(invitationId);
        var request = new AcceptTenantInvitation(invitationId, Uuid.CreateVersion4(), "member@example.com", "token");

        // Assert
        Assert.NotEqual(invitationId, acceptance);
        Assert.Equal(acceptance, TenantInvitationIds.AcceptanceOf(invitationId));
        Assert.Equal(acceptance, ((IIdentifiedRequest)request).RequestId);
        Assert.NotEqual(acceptance, TenantInvitationIds.AcceptanceOf(Uuid.CreateVersion4()));
    }

    [Fact]
    public void ShouldKeepTokenOutOfTheAuthoredIntentGivenAcceptance()
    {
        // Arrange
        var invitationId = Uuid.CreateVersion4();
        var tenantId = Uuid.CreateVersion4();
        var intent = new TenantInvitationAcceptanceIntent(invitationId, tenantId, "member@example.com");

        // Act
        var digest = Bdgrz.Compliance.Features.Kernel.Services.AuthoredIntentDigest.Compute(intent);
        var changed = Bdgrz.Compliance.Features.Kernel.Services.AuthoredIntentDigest.Compute(
            intent with { EmailAddress = "other@example.com" });

        // Assert
        Assert.Equal(64, digest.Length);
        Assert.NotEqual(digest, changed);
        Assert.DoesNotContain(nameof(AcceptTenantInvitation.Token), JsonSerializer.Serialize(intent,
            ComplianceCoreJsonContext.Default.TenantInvitationAcceptanceIntent), StringComparison.OrdinalIgnoreCase);
    }
}
