using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class TenantInvitationTests
{
    static readonly DateTimeOffset Now = TenantInvitationFacts.At;

    [Fact]
    public void ShouldAcceptOnceGivenCurrentUnexpiredToken()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var invitationId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var invitation = new TenantInvitation(tenantId, invitationId);
        var scenario = new AggregateScenario<TenantInvitation>(invitation);
        const string token = "first-token";

        // Act
        var invited = invitation.Invite(invitationId, TenantInvitationFacts.Member("admin@example.com",
            administrator: true), token);
        var wrong = invitation.Accept(invitationId, userId, "admin@example.com", "wrong-token");
        var late = invitation.Accept(invitationId, userId, "admin@example.com", token, Now.AddDays(7));
        var accepted = invitation.Accept(invitationId, userId, "admin@example.com", token);
        var again = invitation.Invite(invitationId, TenantInvitationFacts.Member("admin@example.com", administrator: true),
            token);

        // Assert
        Assert.True(invited.IsSuccess);
        Assert.False(wrong.IsSuccess);
        Assert.False(late.IsSuccess);
        Assert.True(accepted.IsSuccess);
        Assert.False(again.IsSuccess);
        Assert.True(invitation.IsAccepted);
        var acceptedFact = Assert.Single(scenario.PendingEvents.OfType<TenantInvitationAccepted>());
        Assert.Equal("admin@example.com", acceptedFact.EmailAddress);
        Assert.Equal(invitationId, acceptedFact.InvitationId);
        Assert.Equal(TenantInvitationIds.AcceptanceOf(invitationId), acceptedFact.Operation.LogicalId);
    }

    [Fact]
    public void ShouldAcceptUnderAcceptanceIdGivenInvitationCreatedUnderInvitationId()
    {
        // Arrange
        var invitationId = Uuid.CreateVersion4();
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), invitationId);
        var scenario = new AggregateScenario<TenantInvitation>(invitation);

        // Act
        var invited = invitation.Invite(invitationId, TenantInvitationFacts.Member(), "token");
        var accepted = invitation.Accept(invitationId, Uuid.CreateVersion4(), "member@example.com", "token");

        // Assert
        Assert.True(invited.IsSuccess);
        Assert.True(accepted.IsSuccess);
        Assert.Equal(invitationId, Assert.Single(scenario.PendingEvents.OfType<TenantMemberInvited>()).Operation.LogicalId);
        Assert.NotEqual(invitationId, TenantInvitationIds.AcceptanceOf(invitationId));
        Assert.Equal(2, scenario.PendingEvents.OfType<INativeOperationFact>().Count());
    }

    [Fact]
    public void ShouldRejectAdministratorGrantGivenFirmStaffInvitation()
    {
        // Arrange
        var invitationId = Uuid.CreateVersion4();
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), invitationId);

        // Act
        var result = invitation.Invite(invitationId, TenantInvitationFacts.Member("staff@example.com", "firm_staff",
            administrator: true), "token");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
    }

    [Fact]
    public void ShouldRejectMalformedTokenHashGivenInvitationIssue()
    {
        // Arrange
        var invitationId = Uuid.CreateVersion4();
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), invitationId);
        var scenario = new AggregateScenario<TenantInvitation>(invitation);

        // Act
        var result = invitation.Invite(TenantInvitationFacts.Provenance(invitation, invitationId),
            TenantInvitationFacts.Response(), TenantInvitationFacts.Member(), new string('Z', 64), "current",
            Now.AddDays(7), Now, 1, null);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldCarrySelectedRoleGivenClientPersonnelInvitation()
    {
        // Arrange
        var invitationId = Uuid.CreateVersion4();
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), invitationId);
        var scenario = new AggregateScenario<TenantInvitation>(invitation);

        // Act
        var invited = invitation.Invite(invitationId, TenantInvitationFacts.Member(role: BuiltInRbac.ComplianceManagementRole),
            "selected-role-token");
        var accepted = invitation.Accept(invitationId, Uuid.CreateVersion4(), "member@example.com", "selected-role-token");

        // Assert
        Assert.True(invited.IsSuccess);
        Assert.True(accepted.IsSuccess);
        Assert.Equal(BuiltInRbac.ComplianceManagementRole,
            Assert.IsType<TenantMemberInvited>(scenario.PendingEvents[0]).BuiltInRole);
        Assert.Equal(BuiltInRbac.ComplianceManagementRole,
            Assert.IsType<TenantInvitationAccepted>(scenario.PendingEvents[1]).BuiltInRole);
    }

    [Fact]
    public void ShouldCarryGuestScopeManifestAndExpiryGivenScopedGuestInvitation()
    {
        // Arrange
        var invitationId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var programIds = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var membershipExpiresOn = DateOnly.FromDateTime(Now.UtcDateTime).AddDays(30);
        var terms = TenantInvitationFacts.Guest(programIds, membershipExpiresOn);
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), invitationId);
        var scenario = new AggregateScenario<TenantInvitation>(invitation);

        // Act
        var invited = invitation.Invite(invitationId, terms, "guest-invitation-token");
        var accepted = invitation.Accept(invitationId, userId, "guest@example.com", "guest-invitation-token");
        var invitationEvent = Assert.Single(scenario.PendingEvents.OfType<TenantMemberInvited>());
        var acceptedEvent = Assert.Single(scenario.PendingEvents.OfType<TenantInvitationAccepted>());

        // Assert
        Assert.True(invited.IsSuccess, invited.Error?.Message);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal("guest", invitationEvent.Affiliation);
        Assert.False(invitationEvent.Administrator);
        Assert.NotNull(invitationEvent.GuestProgramIdsManifest);
        Assert.Equal(membershipExpiresOn, invitationEvent.GuestExpiresOn);
        Assert.Equal("guest", acceptedEvent.Affiliation);
        Assert.Equal(invitationEvent.GuestProgramIdsManifest!.Sha256, acceptedEvent.GuestProgramIdsManifest!.Sha256);
        Assert.Equal(membershipExpiresOn, acceptedEvent.GuestExpiresOn);
        Assert.Equal(Now, acceptedEvent.AcceptedAt);
        Assert.Equal(terms.InvitedBy, acceptedEvent.InvitedBy);
        Assert.Equal("Org Admin", acceptedEvent.InvitedByDisplay);
    }

    [Fact]
    public void ShouldRejectGuestAcceptanceReplayGivenInvitationTokenWasExpired()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var invitationId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var replay = new TenantInvitation(tenantId, invitationId);
        var scope = TenantInvitationFacts.Scope("TenantMemberInvited.GuestProgramIdsManifest.v1");
        var expiresAt = Now.AddDays(7);
        var guestExpiresOn = DateOnly.FromDateTime(Now.UtcDateTime).AddDays(30);
        var invited = new TenantMemberInvited(tenantId, invitationId, "guest@example.com", "guest", false,
            TenantInvitationFacts.HashOf("guest-token"), expiresAt, Uuid.CreateVersion4(), null, "current", scope,
            guestExpiresOn, Now, "Org Admin", 1,
            TenantInvitationFacts.Provenance(replay, invitationId), null);
        var acceptedAfterExpiry = new TenantInvitationAccepted(tenantId, invitationId, userId, "guest@example.com", "guest",
            false, null, scope, guestExpiresOn, expiresAt, invited.InvitedBy, "Org Admin",
            TenantInvitationFacts.Provenance(replay, TenantInvitationIds.AcceptanceOf(invitationId),
                "AcceptTenantInvitation"), null);

        // Act
        var error = Record.Exception(() => new AggregateScenario<TenantInvitation>(replay).Given(
            DomainEventSeed.Attach(Fresh(invited), replay.Id, 1),
            DomainEventSeed.Attach(acceptedAfterExpiry, replay.Id, 2)));

        // Assert
        Assert.Contains("expired", Assert.IsType<InvalidOperationException>(error).Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShouldRejectGuestInvitationGivenMissingOrInvalidScopeAndExpiry()
    {
        // Arrange
        var invitationId = Uuid.CreateVersion4();
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), invitationId);
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        var programId = Uuid.CreateVersion4();
        var scenario = new AggregateScenario<TenantInvitation>(invitation);

        // Act
        var missingScope = invitation.Invite(invitationId, TenantInvitationFacts.Guest([], today.AddDays(1)), "t");
        var emptyProgram = invitation.Invite(invitationId, TenantInvitationFacts.Guest([Uuid.Empty], today.AddDays(1)), "t");
        var duplicateProgram = invitation.Invite(invitationId,
            TenantInvitationFacts.Guest([programId, programId], today.AddDays(1)), "t");
        var expired = invitation.Invite(invitationId, TenantInvitationFacts.Guest([programId], today), "t");
        var unattributed = invitation.Invite(invitationId,
            TenantInvitationFacts.Guest([programId], today.AddDays(1), display: " "), "t");

        // Assert
        Assert.Equal(RequestErrorKind.Validation, missingScope.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, emptyProgram.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, duplicateProgram.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, expired.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, unattributed.Error?.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectAcceptanceGivenGuestMembershipEndDateHasPassed()
    {
        // Arrange
        var invitationId = Uuid.CreateVersion4();
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), invitationId);
        var scenario = new AggregateScenario<TenantInvitation>(invitation);
        Assert.True(invitation.Invite(invitationId, TenantInvitationFacts.Guest([Uuid.CreateVersion4()],
            DateOnly.FromDateTime(Now.UtcDateTime).AddDays(1)), "guest-expiry-token").IsSuccess);

        // Act
        var result = invitation.Accept(invitationId, Uuid.CreateVersion4(), "guest@example.com", "guest-expiry-token",
            Now.AddDays(2));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.DoesNotContain(scenario.PendingEvents, item => item is TenantInvitationAccepted);
    }

    [Fact]
    public void ShouldRejectRoleGivenUnsupportedValueOrFirmStaff()
    {
        // Arrange
        var firstId = Uuid.CreateVersion4();
        var secondId = Uuid.CreateVersion4();
        var tenantId = Uuid.CreateVersion4();
        var client = new TenantInvitation(tenantId, firstId);
        var firm = new TenantInvitation(tenantId, secondId);

        // Act
        var unknown = client.Invite(firstId, TenantInvitationFacts.Member(role: "unknown_role"), "t");
        var firmRole = firm.Invite(secondId, TenantInvitationFacts.Member("firm@example.com", "firm_staff",
            role: BuiltInRbac.ComplianceParticipationRole), "t");

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(unknown.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(firmRole.Error).Kind);
        Assert.Empty(new AggregateScenario<TenantInvitation>(client).PendingEvents);
        Assert.Empty(new AggregateScenario<TenantInvitation>(firm).PendingEvents);
    }

    [Fact]
    public void ShouldRejectSecondCreationGivenInvitationAlreadyExists()
    {
        // Arrange
        var invitationId = Uuid.CreateVersion4();
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), invitationId);
        Assert.True(invitation.Invite(invitationId, TenantInvitationFacts.Member(), "first").IsSuccess);

        // Act
        var promoted = invitation.Invite(invitationId, TenantInvitationFacts.Member(role: BuiltInRbac.ComplianceManagementRole),
            "second");

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(promoted.Error).Kind);
        Assert.Single(new AggregateScenario<TenantInvitation>(invitation).PendingEvents);
    }

    [Fact]
    public void ShouldRejectAcceptanceGivenWrongRecipientOrUnknownInvitation()
    {
        // Arrange
        var invitationId = Uuid.CreateVersion4();
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), invitationId);
        var unknown = new TenantInvitation(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(invitation.Invite(invitationId, TenantInvitationFacts.Member(), "token").IsSuccess);

        // Act
        var wrongRecipient = invitation.Accept(invitationId, Uuid.CreateVersion4(), "other@example.com", "token");
        var missing = unknown.PrepareAccept(Uuid.CreateVersion4(), "member@example.com", "token", Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(wrongRecipient.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(missing.Error).Kind);
    }

    [Fact]
    public void ShouldReportExpiredGivenTokenLifetimeOrGuestEndDateHasPassed()
    {
        // Arrange
        var invitationId = Uuid.CreateVersion4();
        var guestId = Uuid.CreateVersion4();
        var tenantId = Uuid.CreateVersion4();
        var member = new TenantInvitation(tenantId, invitationId);
        var guest = new TenantInvitation(tenantId, guestId);
        Assert.True(member.Invite(invitationId, TenantInvitationFacts.Member(), "m").IsSuccess);
        Assert.True(guest.Invite(guestId, TenantInvitationFacts.Guest([Uuid.CreateVersion4()],
            DateOnly.FromDateTime(Now.UtcDateTime).AddDays(2)), "g").IsSuccess);

        // Act
        var memberAfterLifetime = member.IsExpiredAt(Now.AddDays(7));
        var memberBefore = member.IsExpiredAt(Now.AddDays(6));
        var guestAfterEnd = guest.IsExpiredAt(Now.AddDays(3));

        // Assert
        Assert.True(memberAfterLifetime);
        Assert.False(memberBefore);
        Assert.True(guestAfterEnd);
        Assert.False(new TenantInvitation(tenantId, Uuid.CreateVersion4()).IsExpiredAt(Now.AddYears(1)));
    }

    [Fact]
    public void ShouldRejectCreationGivenExpiryOtherThanSevenDayLifetime()
    {
        // Arrange
        var invitationId = Uuid.CreateVersion4();
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), invitationId);
        var scenario = new AggregateScenario<TenantInvitation>(invitation);

        // Act
        var longer = invitation.Invite(invitationId, TenantInvitationFacts.Member(), "t", expiresAt: Now.AddDays(30));
        var shorter = invitation.Invite(invitationId, TenantInvitationFacts.Member(), "t", expiresAt: Now.AddDays(1));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(longer.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(shorter.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectCreationGivenForeignOperationOrMissingMailboxClaimOrScope()
    {
        // Arrange
        var invitationId = Uuid.CreateVersion4();
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), invitationId);
        var scenario = new AggregateScenario<TenantInvitation>(invitation);
        var guestTerms = TenantInvitationFacts.Guest([Uuid.CreateVersion4()],
            DateOnly.FromDateTime(Now.UtcDateTime).AddDays(5));

        // Act
        var foreign = invitation.Invite(TenantInvitationFacts.Provenance(invitation, Uuid.CreateVersion4()),
            TenantInvitationFacts.Response(), TenantInvitationFacts.Member(), TenantInvitationFacts.HashOf("t"),
            "current", Now.AddDays(7), Now, 1, null);
        var noClaim = invitation.Invite(invitationId, TenantInvitationFacts.Member(), "t", generation: 0);
        var guestWithoutScope = invitation.Invite(TenantInvitationFacts.Provenance(invitation, invitationId),
            TenantInvitationFacts.Response(), guestTerms, TenantInvitationFacts.HashOf("t"), "current",
            Now.AddDays(7), Now, 1, null);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(foreign.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(noClaim.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(guestWithoutScope.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectSecondAcceptanceGivenInvitationAlreadyAcceptedByAnotherUser()
    {
        // Arrange
        var invitationId = Uuid.CreateVersion4();
        var firstUser = Uuid.CreateVersion4();
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), invitationId);
        var scenario = new AggregateScenario<TenantInvitation>(invitation);
        Assert.True(invitation.Invite(invitationId, TenantInvitationFacts.Member(), "token").IsSuccess);
        Assert.True(invitation.Accept(invitationId, firstUser, "member@example.com", "token").IsSuccess);

        // Act
        var competing = invitation.Accept(invitationId, Uuid.CreateVersion4(), "member@example.com", "token");

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(competing.Error).Kind);
        Assert.Single(scenario.PendingEvents.OfType<TenantInvitationAccepted>());
        Assert.Equal(firstUser, invitation.AcceptedUserId);
    }

    [Fact]
    public void ShouldRejectAcceptanceGivenGuestScopeDiffersFromOffer()
    {
        // Arrange
        var invitationId = Uuid.CreateVersion4();
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), invitationId);
        var scenario = new AggregateScenario<TenantInvitation>(invitation);
        Assert.True(invitation.Invite(invitationId, TenantInvitationFacts.Guest([Uuid.CreateVersion4()],
            DateOnly.FromDateTime(Now.UtcDateTime).AddDays(5)), "guest-token").IsSuccess);
        var widened = TenantInvitationFacts.Scope("TenantMemberInvited.GuestProgramIdsManifest.v1", new string('6', 64));

        // Act
        var result = invitation.Accept(invitationId, Uuid.CreateVersion4(), "guest@example.com", "guest-token",
            scope: widened);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.DoesNotContain(scenario.PendingEvents, item => item is TenantInvitationAccepted);
    }

    [Fact]
    public void ShouldRejectAcceptanceGivenTimeBeforeInvitationWasIssued()
    {
        // Arrange
        var invitationId = Uuid.CreateVersion4();
        var invitation = new TenantInvitation(Uuid.CreateVersion4(), invitationId);
        var scenario = new AggregateScenario<TenantInvitation>(invitation);
        Assert.True(invitation.Invite(invitationId, TenantInvitationFacts.Member(), "token").IsSuccess);

        // Act
        var result = invitation.Accept(invitationId, Uuid.CreateVersion4(), "member@example.com", "token",
            Now.AddMinutes(-1));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.DoesNotContain(scenario.PendingEvents, item => item is TenantInvitationAccepted);
    }

    [Fact]
    public void ShouldReplayOriginalDecisionGivenExactRetryAfterLaterAcceptanceAndConflictGivenChangedIntent()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var invitationId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var original = new TenantInvitation(tenantId, invitationId);
        var originalScenario = new AggregateScenario<TenantInvitation>(original);
        Assert.True(original.Invite(invitationId, TenantInvitationFacts.Member(), "token").IsSuccess);
        Assert.True(original.Accept(invitationId, userId, "member@example.com", "token").IsSuccess);
        var invited = Assert.Single(originalScenario.PendingEvents.OfType<TenantMemberInvited>());
        var accepted = Assert.Single(originalScenario.PendingEvents.OfType<TenantInvitationAccepted>());
        var creationReplay = new TenantInvitation(tenantId, invitationId, invitationId);
        var acceptanceReplay = new TenantInvitation(tenantId, invitationId, TenantInvitationIds.AcceptanceOf(invitationId));

        // Act
        new AggregateScenario<TenantInvitation>(creationReplay).Given(
            DomainEventSeed.Attach(Fresh(invited), creationReplay.Id, 1),
            DomainEventSeed.Attach(Fresh(accepted), creationReplay.Id, 2));
        new AggregateScenario<TenantInvitation>(acceptanceReplay).Given(
            DomainEventSeed.Attach(Fresh(invited), acceptanceReplay.Id, 1),
            DomainEventSeed.Attach(Fresh(accepted), acceptanceReplay.Id, 2));
        var exactCreation = creationReplay.Acceptance.Decide(AcceptedOperationBinding.Of(invited.Operation));
        var changedCreation = creationReplay.Acceptance.Decide(AcceptedOperationBinding.Of(
            invited.Operation with { AuthoredIntentSha256 = new string('c', 64) }));
        var exactAcceptance = acceptanceReplay.Acceptance.Decide(AcceptedOperationBinding.Of(accepted.Operation));

        // Assert
        Assert.True(creationReplay.IsAccepted);
        Assert.Equal(userId, creationReplay.AcceptedUserId);
        Assert.Equal(NativeAcceptanceDecision.Replayed, exactCreation);
        Assert.Equal(NativeAcceptanceDecision.Conflict, changedCreation);
        Assert.Equal(NativeAcceptanceDecision.Replayed, exactAcceptance);
    }

    [Fact]
    public void ShouldSelectNoDecisionGivenUnacceptedOperationOnValidHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var invitationId = Uuid.CreateVersion4();
        var original = new TenantInvitation(tenantId, invitationId);
        var originalScenario = new AggregateScenario<TenantInvitation>(original);
        Assert.True(original.Invite(invitationId, TenantInvitationFacts.Member(), "token").IsSuccess);
        var invited = Assert.Single(originalScenario.PendingEvents.OfType<TenantMemberInvited>());
        var replay = new TenantInvitation(tenantId, invitationId, TenantInvitationIds.AcceptanceOf(invitationId));

        // Act
        new AggregateScenario<TenantInvitation>(replay).Given(DomainEventSeed.Attach(Fresh(invited), replay.Id, 1));
        var decision = replay.Acceptance.Decide(AcceptedOperationBinding.Of(TenantInvitationFacts.Provenance(replay,
            TenantInvitationIds.AcceptanceOf(invitationId), "AcceptTenantInvitation")));

        // Assert
        Assert.True(replay.IsCreated);
        Assert.False(replay.IsAccepted);
        Assert.Equal(NativeAcceptanceDecision.None, decision);
    }

    static TEvent Fresh<TEvent>(TEvent fact)
        where TEvent : DomainEvent => (TEvent)System.Text.Json.JsonSerializer.Deserialize(
            System.Text.Json.JsonSerializer.Serialize(fact, fact.GetType(), ComplianceCoreJsonContext.Default),
            fact.GetType(), ComplianceCoreJsonContext.Default)!;
}
