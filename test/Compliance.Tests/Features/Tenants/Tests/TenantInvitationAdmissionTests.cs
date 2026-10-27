using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class TenantInvitationAdmissionTests
{
    const string Email = "member@example.com";

    [Fact]
    public async Task ShouldFreezeInvitationAndClaimMailboxGivenNewInvitation()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var invitationId = Uuid.CreateVersion4();

        // Act
        var result = await host.InviteMemberAsync(invitationId, Email);
        var invited = await host.InvitedAsync(invitationId);
        var mailbox = await host.MailboxAsync(Email);
        var token = await host.TokenOfAsync(invitationId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(invitationId, invited.InvitationId);
        Assert.Equal(invitationId, invited.Operation.LogicalId);
        Assert.Equal(host.Inviter, invited.InvitedBy);
        Assert.Equal(BuiltInRbac.ComplianceManagementRole, invited.BuiltInRole);
        Assert.Equal(TenantInvitationHost.At.AddDays(7), invited.ExpiresAt);
        Assert.Equal(1, invited.MailboxGeneration);
        Assert.NotNull(invited.FirstResponse);
        Assert.Equal(invitationId, mailbox.Holder);
        Assert.Equal(1, mailbox.HolderGeneration);
        var serialized = JsonSerializer.Serialize(invited, ComplianceCoreJsonContext.Default.TenantMemberInvited);
        Assert.DoesNotContain(token, serialized, StringComparison.Ordinal);
        Assert.Contains(invited.TokenHash, serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldAppendNothingGivenExactInvitationRetry()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var invitationId = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(invitationId, Email)).IsSuccess);
        host.Clock.Advance(TimeSpan.FromHours(3));

        // Act
        var retry = await host.InviteMemberAsync(invitationId, Email);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Single(await host.InvitationFactsAsync(invitationId));
        Assert.Single(await host.MailboxFactsAsync(Email));
    }

    [Theory]
    [InlineData("email")]
    [InlineData("role")]
    [InlineData("actor")]
    public async Task ShouldConflictGivenSameInvitationIdWithChangedRecipientPurposeOrActor(string change)
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var invitationId = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(invitationId, Email)).IsSuccess);

        // Act
        var result = change switch
        {
            "email" => await host.InviteMemberAsync(invitationId, "other@example.com"),
            "role" => await host.InviteMemberAsync(invitationId, Email, BuiltInRbac.ComplianceParticipationRole),
            _ => await host.InviteMemberAsync(invitationId, Email, inviter: Uuid.CreateVersion4()),
        };

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Single(await host.InvitationFactsAsync(invitationId));
        Assert.Empty(await host.MailboxFactsAsync("other@example.com"));
    }

    [Fact]
    public async Task ShouldYieldOneClaimWinnerGivenConcurrentInvitationIdsForTheSameMailbox()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var ids = Enumerable.Range(0, 6).Select(_ => Uuid.CreateVersion4()).ToArray();

        // Act
        var results = await Task.WhenAll(ids.Select(id => Task.Run(() => host.InviteMemberAsync(id, Email))));
        var mailbox = await host.MailboxAsync(Email);

        // Assert
        var winners = ids.Zip(results).Where(pair => pair.Second.IsSuccess).Select(pair => pair.First).ToArray();
        var winner = Assert.Single(winners);
        Assert.All(results.Where(result => !result.IsSuccess),
            result => Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind));
        Assert.Equal(winner, mailbox.Holder);
        Assert.Single(await host.MailboxFactsAsync(Email));
        foreach (var id in ids.Where(id => id != winner))
            Assert.Empty(await host.InvitationFactsAsync(id));
    }

    [Fact]
    public async Task ShouldConflictGivenSecondPendingInvitationForTheMailbox()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        Assert.True((await host.InviteMemberAsync(Uuid.CreateVersion4(), Email)).IsSuccess);
        var second = Uuid.CreateVersion4();

        // Act
        var result = await host.InviteMemberAsync(second, Email);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(await host.InvitationFactsAsync(second));
    }

    [Fact]
    public async Task ShouldClaimNextGenerationGivenPriorInvitationExpired()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(first, Email)).IsSuccess);
        host.Clock.Advance(TenantInvitation.Lifetime + TimeSpan.FromMinutes(1));

        // Act
        var result = await host.InviteMemberAsync(second, Email);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, (await host.InvitedAsync(second)).MailboxGeneration);
        Assert.Equal(second, (await host.MailboxAsync(Email)).Holder);
        Assert.Equal(TenantInvitationMailboxClaim.SupersededReason,
            (await host.MailboxFactsAsync(Email)).OfType<TenantInvitationMailboxReleased>().Single().Reason);
    }

    [Fact]
    public async Task ShouldReleaseClaimAndAllowLaterInvitationGivenAcceptance()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(first, Email)).IsSuccess);
        var accepted = await host.AcceptAsync(first, Email, await host.TokenOfAsync(first), userId);

        // Act
        var afterAcceptance = await host.MailboxAsync(Email);
        var later = await host.InviteMemberAsync(second, Email);

        // Assert
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(Uuid.Empty, afterAcceptance.Holder);
        Assert.True(later.IsSuccess, later.Error?.Message);
        Assert.Equal(2, (await host.InvitedAsync(second)).MailboxGeneration);
        Assert.Equal([TenantInvitationMailboxClaim.AcceptedReason],
            (await host.MailboxFactsAsync(Email)).OfType<TenantInvitationMailboxReleased>().Select(fact => fact.Reason));
    }

    [Fact]
    public async Task ShouldReplayOriginalAcceptanceGivenExactAcceptAfterExpiry()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var invitationId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(invitationId, Email)).IsSuccess);
        var token = await host.TokenOfAsync(invitationId);
        Assert.True((await host.AcceptAsync(invitationId, Email, token, userId)).IsSuccess);
        host.Clock.Advance(TimeSpan.FromDays(30));

        // Act
        var retry = await host.AcceptAsync(invitationId, Email, token, userId);
        var fresh = await host.AcceptAsync(Uuid.CreateVersion4(), Email, token, userId);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.False(fresh.IsSuccess);
        Assert.Single((await host.InvitationFactsAsync(invitationId)).OfType<TenantInvitationAccepted>());
    }

    [Fact]
    public async Task ShouldRejectAcceptanceGivenInvitationExpiredBeforeFirstAcceptance()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var invitationId = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(invitationId, Email)).IsSuccess);
        var token = await host.TokenOfAsync(invitationId);
        host.Clock.Advance(TenantInvitation.Lifetime);

        // Act
        var result = await host.AcceptAsync(invitationId, Email, token, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty((await host.InvitationFactsAsync(invitationId)).OfType<TenantInvitationAccepted>());
        Assert.Equal(invitationId, (await host.MailboxAsync(Email)).Holder);
    }

    [Fact]
    public async Task ShouldConflictGivenAcceptanceByAnotherUser()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var invitationId = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(invitationId, Email)).IsSuccess);
        var token = await host.TokenOfAsync(invitationId);
        Assert.True((await host.AcceptAsync(invitationId, Email, token, Uuid.CreateVersion4())).IsSuccess);

        // Act
        var other = await host.AcceptAsync(invitationId, Email, token, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, other.Error?.Kind);
        Assert.Single((await host.InvitationFactsAsync(invitationId)).OfType<TenantInvitationAccepted>());
    }

    [Fact]
    public async Task ShouldConflictGivenSameAcceptanceWithChangedRecipient()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var invitationId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(invitationId, Email)).IsSuccess);
        var token = await host.TokenOfAsync(invitationId);
        Assert.True((await host.AcceptAsync(invitationId, Email, token, userId)).IsSuccess);

        // Act
        var changed = await host.AcceptAsync(invitationId, "other@example.com", token, userId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectGivenTokenOfAnotherInvitationOrUnknownInvitation()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(first, Email)).IsSuccess);
        Assert.True((await host.InviteMemberAsync(second, "second@example.com")).IsSuccess);
        var secondToken = await host.TokenOfAsync(second);

        // Act
        var mismatched = await host.AcceptAsync(first, Email, secondToken, Uuid.CreateVersion4());
        var unknown = await host.AcceptAsync(Uuid.CreateVersion4(), Email, secondToken, Uuid.CreateVersion4());
        var wrongTenant = await host.AcceptAsync(first, Email, await host.TokenOfAsync(first), Uuid.CreateVersion4(),
            Uuid.CreateVersion4());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, mismatched.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, unknown.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, wrongTenant.Error?.Kind);
        Assert.Empty((await host.InvitationFactsAsync(first)).OfType<TenantInvitationAccepted>());
        Assert.Empty((await host.InvitationFactsAsync(second)).OfType<TenantInvitationAccepted>());
    }

    [Fact]
    public async Task ShouldLeaveLaterGenerationHeldGivenLateRetryOfOldAcceptance()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(first, Email)).IsSuccess);
        var token = await host.TokenOfAsync(first);
        Assert.True((await host.AcceptAsync(first, Email, token, userId)).IsSuccess);
        Assert.True((await host.InviteMemberAsync(second, Email)).IsSuccess);

        // Act
        var lateRetry = await host.AcceptAsync(first, Email, token, userId);
        var mailbox = await host.MailboxAsync(Email);

        // Assert
        Assert.True(lateRetry.IsSuccess);
        Assert.Equal(second, mailbox.Holder);
        Assert.Equal(2, mailbox.HolderGeneration);
    }

    [Fact]
    public async Task ShouldContinueGivenRetryOfInvitationWhoseClaimWasAcquiredBeforeItsFact()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var invitationId = Uuid.CreateVersion4();
        await using (var scope = host.Provider.CreateAsyncScope())
        {
            var claimed = await scope.ServiceProvider.GetRequiredService<TenantInvitationMailboxes>().AcquireAsync(
                new RequestDispatchContext(RequestActor.System), host.TenantId, Email, invitationId, CancellationToken.None);
            Assert.Equal(1, claimed.Value);
        }

        // Act
        var other = await host.InviteMemberAsync(Uuid.CreateVersion4(), Email);
        var retry = await host.InviteMemberAsync(invitationId, Email);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, other.Error?.Kind);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(1, (await host.InvitedAsync(invitationId)).MailboxGeneration);
        Assert.Single(await host.MailboxFactsAsync(Email));
    }

    [Fact]
    public async Task ShouldSupersedeClaimGivenInvitationWasNeverPublishedWithinLease()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var abandoned = Uuid.CreateVersion4();
        var next = Uuid.CreateVersion4();
        await using (var scope = host.Provider.CreateAsyncScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<TenantInvitationMailboxes>().AcquireAsync(
                new RequestDispatchContext(RequestActor.System), host.TenantId, Email, abandoned,
                CancellationToken.None)).IsSuccess);
        host.Clock.Advance(TenantInvitationMailboxes.UnpublishedLease + TimeSpan.FromSeconds(1));

        // Act
        var result = await host.InviteMemberAsync(next, Email);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(next, (await host.MailboxAsync(Email)).Holder);
    }

    [Fact]
    public async Task ShouldNotClaimMailboxGivenInvalidTerms()
    {
        // Arrange
        await using var host = new TenantInvitationHost();

        // Act
        var badRole = await host.InviteMemberAsync(Uuid.CreateVersion4(), Email, "unknown_role");
        var badEmail = await host.InviteMemberAsync(Uuid.CreateVersion4(), "not-an-email");

        // Assert
        Assert.Equal(RequestErrorKind.Validation, badRole.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, badEmail.Error?.Kind);
        Assert.Empty(await host.MailboxFactsAsync(Email));
    }

    [Fact]
    public async Task ShouldFreezeGuestScopeManifestGivenGuestInvitationAndAcceptance()
    {
        // Arrange
        var programs = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        await using var host = new TenantInvitationHost(programs: programs);
        var invitationId = Uuid.CreateVersion4();
        var expiresOn = DateOnly.FromDateTime(TenantInvitationHost.At.UtcDateTime).AddDays(30);
        var ordered = programs.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray();

        // Act
        var invited = await host.InviteGuestAsync(invitationId, "guest@example.com", programs, expiresOn);
        var fact = await host.InvitedAsync(invitationId);
        var accepted = await host.AcceptAsync(invitationId, "guest@example.com", await host.TokenOfAsync(invitationId),
            Uuid.CreateVersion4());
        var acceptedFact = (await host.InvitationFactsAsync(invitationId)).OfType<TenantInvitationAccepted>().Single();
        var offeredScope = await host.Content.ReadJsonAsync<IReadOnlyList<Uuid>>(host.TenantId.ToString(),
            fact.GuestProgramIdsManifest!);
        var acceptedScope = await host.Content.ReadJsonAsync<IReadOnlyList<Uuid>>(host.TenantId.ToString(),
            acceptedFact.GuestProgramIdsManifest!);

        // Assert
        Assert.True(invited.IsSuccess, invited.Error?.Message);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(ordered, offeredScope);
        Assert.Equal(ordered, acceptedScope);
        Assert.Equal("TenantMemberInvited.GuestProgramIdsManifest.v1", fact.GuestProgramIdsManifest!.Schema);
        Assert.Equal("TenantInvitationAccepted.GuestProgramIdsManifest.v1", acceptedFact.GuestProgramIdsManifest!.Schema);
        Assert.Equal(expiresOn, acceptedFact.GuestExpiresOn);
    }

    [Fact]
    public async Task ShouldConflictGivenSameGuestInvitationIdWithChangedScopeOrExpiry()
    {
        // Arrange
        var programs = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        await using var host = new TenantInvitationHost(programs: programs);
        var invitationId = Uuid.CreateVersion4();
        var expiresOn = DateOnly.FromDateTime(TenantInvitationHost.At.UtcDateTime).AddDays(30);
        Assert.True((await host.InviteGuestAsync(invitationId, "guest@example.com", programs, expiresOn)).IsSuccess);

        // Act
        var reordered = await host.InviteGuestAsync(invitationId, "guest@example.com", [programs[1], programs[0]], expiresOn);
        var narrowed = await host.InviteGuestAsync(invitationId, "guest@example.com", [programs[0]], expiresOn);
        var extended = await host.InviteGuestAsync(invitationId, "guest@example.com", programs, expiresOn.AddDays(1));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, narrowed.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, extended.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, reordered.Error?.Kind);
        Assert.Single(await host.InvitationFactsAsync(invitationId));
    }

    [Fact]
    public async Task ShouldRejectGuestInvitationGivenProgramOutsideTenant()
    {
        // Arrange
        await using var host = new TenantInvitationHost(programs: [Uuid.CreateVersion4()]);

        // Act
        var result = await host.InviteGuestAsync(Uuid.CreateVersion4(), "guest@example.com", [Uuid.CreateVersion4()],
            DateOnly.FromDateTime(TenantInvitationHost.At.UtcDateTime).AddDays(30));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(await host.MailboxFactsAsync("guest@example.com"));
    }
}
