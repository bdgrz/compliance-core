using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
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
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;

/// <summary>The identity slot protection a recovery holds: who may associate the slot and when the hold ends.</summary>
public sealed class UserIdentityRecoveryProtectionTests
{
    [Fact]
    public void ShouldRejectRegistrationGivenSlotProtectedForAnotherAccount()
    {
        // Arrange
        var account = Uuid.CreateVersion4();
        var identity = new UserIdentity("issuer", "replacement");
        Protect(identity, Uuid.CreateVersion4(), account, RecoveryIdentityRole.Replacement);

        // Act
        var other = identity.Register(Uuid.CreateVersion4(), null);
        var fresh = identity.Register(null, null);

        // Assert
        Assert.False(other.IsSuccess);
        Assert.Equal(UserIdentity.ProtectedForRecovery, other.Error);
        Assert.False(fresh.IsSuccess);
        Assert.False(identity.IsRegistered);
    }

    [Fact]
    public void ShouldAllowRegistrationGivenSlotProtectedForTheSameAccount()
    {
        // Arrange
        var account = Uuid.CreateVersion4();
        var identity = new UserIdentity("issuer", "replacement");
        Protect(identity, Uuid.CreateVersion4(), account, RecoveryIdentityRole.Replacement);

        // Act
        var registered = identity.Register(account, null);

        // Assert
        Assert.True(registered.IsSuccess);
        Assert.Equal(account, identity.UserId);
    }

    [Fact]
    public void ShouldKeepHoldGivenSettlementOfAnotherChallenge()
    {
        // Arrange
        var account = Uuid.CreateVersion4();
        var held = Uuid.CreateVersion4();
        var identity = new UserIdentity("issuer", "replacement", held);
        Protect(identity, held, account, RecoveryIdentityRole.Replacement);

        // Act
        Settle(identity, Uuid.CreateVersion4());
        var stillHeld = identity.Register(Uuid.CreateVersion4(), null);
        Settle(identity, held);
        var released = identity.Register(Uuid.CreateVersion4(), null);

        // Assert
        Assert.False(stillHeld.IsSuccess);
        Assert.True(released.IsSuccess);
        Assert.True(identity.IsSettledFor(held));
        Assert.False(identity.IsProtectedFor(held));
    }

    [Fact]
    public void ShouldFenceDelayedProtectionGivenChallengeAlreadySettledOnTheSlot()
    {
        // Arrange
        var account = Uuid.CreateVersion4();
        var challenge = Uuid.CreateVersion4();
        var identity = new UserIdentity("issuer", "replacement", challenge);
        Settle(identity, challenge);

        // Act
        var decision = identity.DecideProtection(challenge, account, RecoveryIdentityRole.Replacement,
            Uuid.CreateVersion4());
        var protect = identity.Protect(EmailAddressFacts.Operation(identity.Stream, Uuid.CreateVersion4(),
                "ProtectRecoveryIdentity"), EmailAddressFacts.Content, null, challenge, account,
            RecoveryIdentityRole.Replacement, Uuid.CreateVersion4(), 1);

        // Assert
        Assert.Equal(UserIdentity.ProtectionOutcome.Fenced, decision.Outcome);
        Assert.False(protect.IsSuccess);
        Assert.False(identity.IsProtectedFor(challenge));
    }

    [Fact]
    public void ShouldWaitGivenAnotherRecoveryOfTheSameAccountAndConflictGivenAnotherAccount()
    {
        // Arrange
        var account = Uuid.CreateVersion4();
        var identity = new UserIdentity("issuer", "replacement");
        Protect(identity, Uuid.CreateVersion4(), account, RecoveryIdentityRole.Replacement);

        // Act
        var sameAccount = identity.DecideProtection(Uuid.CreateVersion4(), account, RecoveryIdentityRole.Replacement,
            Uuid.CreateVersion4());
        var otherAccount = identity.DecideProtection(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            RecoveryIdentityRole.Replacement, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(UserIdentity.ProtectionOutcome.Wait, sameAccount.Outcome);
        Assert.Equal(UserIdentity.ProtectionOutcome.Conflict, otherAccount.Outcome);
    }

    [Theory]
    [InlineData("replacement-other-account", true)]
    [InlineData("replacement-same-account", false)]
    [InlineData("replacement-revoked", true)]
    [InlineData("old-other-account", true)]
    [InlineData("old-unregistered", true)]
    [InlineData("old-same-account", false)]
    [InlineData("old-revoked-elsewhere", true)]
    [InlineData("old-revoked-to-replacement", false)]
    public void ShouldDecideConflictOnlyGivenDefiniteCompetingAssociation(string scenario, bool conflict)
    {
        // Arrange
        var account = Uuid.CreateVersion4();
        var replacement = Uuid.CreateVersion4();
        var identity = new UserIdentity("issuer", "slot");
        var role = scenario.StartsWith("old", StringComparison.Ordinal)
            ? RecoveryIdentityRole.Old
            : RecoveryIdentityRole.Replacement;
        switch (scenario)
        {
            case "replacement-other-account" or "old-other-account":
                Assert.True(identity.Register(Uuid.CreateVersion4(), null).IsSuccess);
                break;
            case "replacement-same-account" or "old-same-account":
                Assert.True(identity.Register(account, null).IsSuccess);
                break;
            case "replacement-revoked" or "old-revoked-elsewhere":
                Assert.True(identity.Register(account, null).IsSuccess);
                Assert.True(identity.Revoke(Uuid.CreateVersion4(), DateTimeOffset.UtcNow).IsSuccess);
                break;
            case "old-revoked-to-replacement":
                Assert.True(identity.Register(account, null).IsSuccess);
                Assert.True(identity.Revoke(replacement, DateTimeOffset.UtcNow).IsSuccess);
                break;
        }

        // Act
        var decision = identity.DecideProtection(Uuid.CreateVersion4(), account, role, replacement);

        // Assert
        Assert.Equal(conflict ? UserIdentity.ProtectionOutcome.Conflict : UserIdentity.ProtectionOutcome.Proceed,
            decision.Outcome);
    }

    [Fact]
    public void ShouldRejectPublicRevocationGivenProtectedSlot()
    {
        // Arrange
        var account = Uuid.CreateVersion4();
        var identity = new UserIdentity("issuer", "old");
        Assert.True(identity.Register(account, null).IsSuccess);
        Protect(identity, Uuid.CreateVersion4(), account, RecoveryIdentityRole.Old);

        // Act
        var revoked = identity.Revoke(Uuid.CreateVersion4(), DateTimeOffset.UtcNow);

        // Assert
        Assert.False(revoked.IsSuccess);
        Assert.Equal(UserIdentity.ProtectedForRecovery, revoked.Error);
        Assert.False(identity.IsRevoked);
    }

    [Fact]
    public async Task ShouldRefuseContinuationGivenReplacementProtectedForTheRecovery()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var account = await host.SeedAccountAsync();
        var challenge = Uuid.CreateVersion4();
        var token = await host.StartedAsync(account, challenge);
        Assert.True((await host.CompleteAsync(account, challenge, token)).IsSuccess);
        var process = await host.PrepareProcessAsync(account, challenge);
        var materialized = Assert.IsType<ReceiverOutcome.Applied>(
            (await host.SendAsync(await host.CommandAsync(process,
                IdentityRecoveryReceivers.MaterializeIdentityRecoveryPreparation))).Value);
        process = process with
        {
            Facts = new Dictionary<string, NativeFactReference>(process.Facts)
            {
                [ProcessRecord.FactKey(
                    IdentityRecoveryReceivers.MaterializeIdentityRecoveryPreparation, null)] = materialized.Fact,
            },
        };
        var slotId = UserIdentity.GetIdentityId(IdentityRecoveryHost.NewProvider, "new-subject");
        Assert.IsType<ReceiverOutcome.Applied>(
            (await host.SendAsync(await host.CommandAsync(process, IdentityRecoveryReceivers.ProtectRecoveryIdentity,
                slotId.ToString()))).Value);
        var continuation = new UserIdentityContinuation(host.Provider.GetRequiredService<IAggregateExecutor>());
        var system = new RequestDispatchContext(RequestActor.System);

        // Act
        var stranger = await continuation.ContinueAsync(IdentityRecoveryHost.NewProvider, "new-subject", null, null,
            system, CancellationToken.None);
        var owner = await continuation.ContinueAsync(IdentityRecoveryHost.NewProvider, "new-subject", null,
            account.UserId, system, CancellationToken.None);

        // Assert
        Assert.False(stranger.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, stranger.Error.Kind);
        Assert.True(owner.IsSuccess);
        Assert.Equal(account.UserId, owner.Value.UserId);
    }

    static void Protect(UserIdentity identity, Uuid challenge, Uuid account, RecoveryIdentityRole role) =>
        Assert.True(identity.Protect(EmailAddressFacts.Operation(identity.Stream, Uuid.CreateVersion4(),
            "ProtectRecoveryIdentity"), EmailAddressFacts.Content, null, challenge, account, role,
            Uuid.CreateVersion4(), 1).IsSuccess);

    static void Settle(UserIdentity identity, Uuid challenge) =>
        Assert.True(identity.Settle(EmailAddressFacts.Operation(identity.Stream, Uuid.CreateVersion4(),
            "SettleRecoveryIdentityProtection"), EmailAddressFacts.Content, null, challenge, 1).IsSuccess);
}
