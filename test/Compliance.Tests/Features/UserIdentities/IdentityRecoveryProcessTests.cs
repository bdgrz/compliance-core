using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Content;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

/// <summary>
///     The ordered identity recovery process and its ten receivers: the registered definition equals the design
///     catalog, a recovery completes automatically once both proofs are accepted, a competing association before
///     protection rejects the preparation, and every receiver replays, supersedes, waits or refuses as the receiver
///     contract says.
/// </summary>
public sealed class IdentityRecoveryProcessTests
{
    static readonly string CatalogPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "docs", "product", "audits", "2026-10-23", "target-reactor-contracts.json");

    const string NewSubject = "new-subject";

    static Uuid NewIdentity => UserIdentity.GetIdentityId(IdentityRecoveryHost.NewProvider, NewSubject);

    [Fact]
    public void ShouldEqualDesignCatalogGivenRegisteredIdentityRecoveryProcess()
    {
        // Arrange
        using var document = JsonDocument.Parse(File.ReadAllText(CatalogPath));
        var entry = document.RootElement.GetProperty("processes").EnumerateArray()
            .Single(process => process.GetProperty("process").GetString() == IdentityRecoveryProcess.Name);

        // Act
        var definition = IdentityRecoveryProcess.Definition;

        // Assert
        Assert.True(definition.Ordered);
        Assert.Equal(entry.GetProperty("ordered").GetBoolean(), definition.Ordered);
        Assert.Equal(entry.GetProperty("rootSourceEvents").EnumerateArray().Select(root => root.GetString()),
            definition.RootSourceEvents);
        Assert.Equal(entry.GetProperty("stages").EnumerateArray().Select(stage => stage.GetProperty("stage").GetString()),
            definition.Stages.Select(stage => stage.Stage));
        Assert.Equal(entry.GetProperty("stages").EnumerateArray().Select(stage => stage.GetProperty("receivers")
                .EnumerateArray().Select(receiver => receiver.GetString()).ToArray()),
            definition.Stages.Select(stage => stage.Receivers.ToArray()));
        Assert.Equal(entry.GetProperty("abort").EnumerateArray().Select(abort => abort.GetString()), definition.Abort);
    }

    [Fact]
    public void ShouldMapEveryCatalogReactorNameToItsReceiverGivenIdentityRecovery()
    {
        // Arrange
        using var document = JsonDocument.Parse(File.ReadAllText(CatalogPath));
        var reactors = document.RootElement.GetProperty("reactors").EnumerateArray()
            .Where(reactor => reactor.TryGetProperty("processes", out var processes) &&
                              processes.EnumerateArray().Any(process => process.GetString() == IdentityRecoveryProcess.Name))
            .ToDictionary(reactor => reactor.GetProperty("reactor").GetString()!,
                reactor => reactor.GetProperty("command").GetString()!);

        // Act
        var mapped = reactors.Keys.ToDictionary(name => name, IdentityRecoveryReactors.ReceiverOf);

        // Assert
        Assert.Equal(reactors, mapped);
    }

    [Fact]
    public async Task ShouldCompleteRecoveryAutomaticallyGivenBothProofsAndTheProcess()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, challenge) = await ClaimAsync(host);

        // Act
        var record = await host.DriveAsync(account);

        // Assert
        Assert.Equal(ProcessState.Completed, record.State);
        var recovery = await host.FactsAsync(new IdentityRecovery(challenge).Stream);
        Assert.Equal(["IdentityRecoveryPrepared", "IdentityRecoveryAccepted", "IdentityRecoveryCompleted"],
            recovery.Select(fact => fact.GetType().Name));
        var old = await host.IdentityAsync(account.OldIdentityId, challenge);
        var replacement = await host.IdentityAsync(NewIdentity, challenge);
        Assert.True(old.IsRevoked);
        Assert.Equal(NewIdentity, old.ReplacementIdentityId);
        Assert.True(replacement.IsRegistered);
        Assert.Equal(account.UserId, replacement.UserId);
        Assert.False(old.Authenticate().IsSuccess);
        Assert.True(replacement.Authenticate().IsSuccess);
        Assert.False(old.IsProtectedFor(challenge));
        Assert.False(replacement.IsProtectedFor(challenge));
        Assert.True(old.IsSettledFor(challenge));
        Assert.True(replacement.IsSettledFor(challenge));
        var address = await host.AddressAsync(selectedChallengeId: challenge);
        Assert.True(address.IsRecoveryChallengeCompleted(challenge));
        Assert.True(address.IsRecoveryCompleted);
        var notice = Assert.Single(host.Delivery.Notices);
        Assert.Equal((challenge, account.UserId, account.Email), notice);
    }

    [Fact]
    public async Task ShouldRegisterRecoveredIdentityWithRecoveryProvenanceGivenAcceptedSwap()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, challenge) = await ClaimAsync(host, assertedEmail: "asserted@example.com");

        // Act
        await host.DriveAsync(account);

        // Assert
        var registered = Assert.Single((await host.FactsAsync(new UserIdentity(NewIdentity).Stream))
            .OfType<UserIdentityRegistered>());
        Assert.Equal(challenge, registered.RecoveryChallengeId);
        Assert.Equal("asserted@example.com", registered.EmailAddress);
        Assert.Equal("RegisterRecoveredIdentity", registered.Operation!.CommandKind);
        var revoked = Assert.Single((await host.FactsAsync(new UserIdentity(account.OldIdentityId).Stream))
            .OfType<UserIdentityRevoked>());
        Assert.Equal(challenge, revoked.RecoveryChallengeId);
        Assert.Equal(NewIdentity, revoked.ReplacementIdentityId);
        Assert.Equal("RevokeRecoveredOldIdentity", revoked.Operation!.CommandKind);
    }

    [Fact]
    public async Task ShouldRejectPreparationGivenCompetingReplacementAssociationBeforeProtection()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, challenge) = await ClaimAsync(host);
        var record = await host.PrepareProcessAsync(account);
        var prepared = await host.SendAsync(await host.CommandAsync(record,
            IdentityRecoveryReceivers.MaterializeIdentityRecoveryPreparation));
        Assert.IsType<ReceiverOutcome.Applied>(prepared.Value);
        var competitor = await host.RegisterElsewhereAsync(IdentityRecoveryHost.NewProvider, NewSubject);

        // Act
        var driven = await host.DriveAsync(account);

        // Assert
        Assert.Equal(ProcessState.Aborted, driven.State);
        var recovery = await host.FactsAsync(new IdentityRecovery(challenge).Stream);
        Assert.Equal(["IdentityRecoveryPrepared", "IdentityRecoveryPreparationRejected"],
            recovery.Select(fact => fact.GetType().Name));
        Assert.DoesNotContain(recovery, fact => fact is IdentityRecoveryAccepted);
        var old = await host.IdentityAsync(account.OldIdentityId, challenge);
        var replacement = await host.IdentityAsync(NewIdentity, challenge);
        Assert.False(old.IsRevoked);
        Assert.Equal(competitor, replacement.UserId);
        Assert.True(old.IsSettledFor(challenge));
        Assert.True(replacement.IsSettledFor(challenge));
        Assert.False(old.IsProtectedFor(challenge));
        Assert.False(replacement.IsProtectedFor(challenge));
        Assert.Empty((await host.FactsAsync(new UserIdentity(account.OldIdentityId).Stream)).OfType<UserIdentityRevoked>());
        Assert.Empty(host.Delivery.Notices);
        Assert.False((await host.AddressAsync()).IsRecoveryChallengeCompleted(challenge));
    }

    [Fact]
    public async Task ShouldRejectPreparationAndFenceStaleDeliveriesGivenCompetingReplacementAssociationBeforeProtection()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, challenge) = await ClaimAsync(host);
        await host.PrepareProcessAsync(account);
        var protectOld = await host.CommandAsync(await RunUntilAsync(host, challenge,
                IdentityRecoveryReceivers.ProtectRecoveryIdentity),
            IdentityRecoveryReceivers.ProtectRecoveryIdentity, account.OldIdentityId.ToString());
        await host.RegisterElsewhereAsync(IdentityRecoveryHost.NewProvider, NewSubject);
        var driven = await host.DriveAsync(account);
        var withFacts = (await host.ProcessAsync(challenge))!;

        // Act
        var delayedProtect = await host.SendAsync(await host.CommandAsync(withFacts,
            IdentityRecoveryReceivers.ProtectRecoveryIdentity, NewIdentity.ToString()));
        var delayedOldProtect = await host.SendAsync(protectOld);
        var recovery = await host.RecoveryAsync(challenge);

        // Assert
        Assert.Equal(ProcessState.Aborted, driven.State);
        Assert.Equal(IdentityRecovery.Stage.Rejected, recovery.State);
        Assert.IsType<ReceiverOutcome.Superseded>(delayedProtect.Value);
        Assert.True(delayedOldProtect.Value is ReceiverOutcome.Replayed or ReceiverOutcome.Superseded);
        Assert.False((await host.IdentityAsync(NewIdentity, challenge)).IsProtectedFor(challenge));
        Assert.DoesNotContain(await host.FactsAsync(recovery.Stream), fact => fact is IdentityRecoveryAccepted);
    }

    [Fact]
    public async Task ShouldFenceDelayedAcceptanceGivenRejectedPreparationAfterBothSlotsAreProtected()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, challenge) = await ClaimAsync(host);
        await host.PrepareProcessAsync(account);
        var record = await RunUntilAsync(host, challenge, IdentityRecoveryReceivers.AcceptProtectedIdentityRecovery);
        var accept = await host.CommandAsync(record, IdentityRecoveryReceivers.AcceptProtectedIdentityRecovery);
        var recovery = await host.RecoveryAsync(challenge);
        var operation = EmailAddressFactsOperation(recovery.Stream, challenge);
        await host.SeedAsync(new IdentityRecovery(challenge), current =>
            current.Reject(operation, recovery.Intent!, EmailAddressFacts.Content));

        // Act
        var delayed = await host.SendAsync(accept);

        // Assert
        var superseded = Assert.IsType<ReceiverOutcome.Superseded>(delayed.Value);
        Assert.Contains("rejected", superseded.Reason, StringComparison.Ordinal);
        Assert.Equal(IdentityRecovery.Stage.Rejected, (await host.RecoveryAsync(challenge)).State);
        Assert.DoesNotContain(await host.FactsAsync(recovery.Stream), fact => fact is IdentityRecoveryAccepted);
    }

    [Fact]
    public async Task ShouldNotOverwriteRejectedChallengeGivenLaterChallenge()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, first) = await ClaimAsync(host);
        var record = await host.PrepareProcessAsync(account);
        Assert.IsType<ReceiverOutcome.Applied>((await host.SendAsync(await host.CommandAsync(record,
            IdentityRecoveryReceivers.MaterializeIdentityRecoveryPreparation))).Value);
        await host.RegisterElsewhereAsync(IdentityRecoveryHost.NewProvider, NewSubject);
        await host.DriveAsync(account);
        host.Clock.Advance(TimeSpan.FromMinutes(16));

        // Act
        var second = Uuid.CreateVersion4();
        var token = await host.StartedAsync(account, second);
        var claimed = await host.CompleteAsync(account, second, token, subject: "other-new-subject");
        var secondDriven = await host.DriveAsync(account, "second", second);

        // Assert
        Assert.True(claimed.IsSuccess, claimed.Error?.Message);
        Assert.Equal(ProcessState.Completed, secondDriven.State);
        Assert.Equal(IdentityRecovery.Stage.Completed, (await host.RecoveryAsync(second)).State);
        Assert.Equal(IdentityRecovery.Stage.Rejected, (await host.RecoveryAsync(first)).State);
    }

    [Fact]
    public async Task ShouldCompleteAcceptedSwapGivenReplacementAlreadyRegisteredAndNoNewEvent()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, challenge) = await ClaimAsync(host);
        await host.SeedAsync(new UserIdentity(IdentityRecoveryHost.NewProvider, NewSubject),
            identity => identity.Register(account.UserId, null));
        var registrationFacts = (await host.FactsAsync(new UserIdentity(NewIdentity).Stream)).Count;
        var record = await host.PrepareProcessAsync(account);

        // Act
        var driven = await host.DriveAsync(account);

        // Assert
        Assert.Equal(ProcessState.Completed, driven.State);
        Assert.Single((await host.FactsAsync(new UserIdentity(NewIdentity).Stream)).OfType<UserIdentityRegistered>());
        var replacementFacts = await host.FactsAsync(new UserIdentity(NewIdentity).Stream);
        Assert.Equal(["RecoveryIdentityProtected", "RecoveryIdentityProtectionSettled"],
            replacementFacts.Skip(registrationFacts).Select(fact => fact.GetType().Name));
        var registration = driven.Facts[ProcessRecord.FactKey(IdentityRecoveryReceivers.RegisterRecoveredIdentity, null)];
        Assert.Equal(replacementFacts[0].Metadata.EventId, registration.EventId);
        Assert.Equal(IdentityRecovery.Stage.Completed, (await host.RecoveryAsync(challenge)).State);
        Assert.True((await host.IdentityAsync(account.OldIdentityId, challenge)).IsRevoked);
        Assert.Equal(record.Key, driven.Key);
    }

    [Fact]
    public async Task ShouldReuseExistingRevocationProofGivenOldIdentityAlreadyRevokedToTheSameReplacement()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, challenge) = await ClaimAsync(host);
        await host.SeedAsync(new UserIdentity(IdentityRecoveryHost.NewProvider, NewSubject),
            identity => identity.Register(account.UserId, null));
        await host.SeedAsync(new UserIdentity(account.OldIdentityId),
            identity => identity.Revoke(NewIdentity, DateTimeOffset.UtcNow));
        var revocations = (await host.FactsAsync(new UserIdentity(account.OldIdentityId).Stream))
            .OfType<UserIdentityRevoked>().Count();

        // Act
        var driven = await host.DriveAsync(account);

        // Assert
        Assert.Equal(ProcessState.Completed, driven.State);
        Assert.Equal(revocations, (await host.FactsAsync(new UserIdentity(account.OldIdentityId).Stream))
            .OfType<UserIdentityRevoked>().Count());
        Assert.Equal(IdentityRecovery.Stage.Completed, (await host.RecoveryAsync(challenge)).State);
    }

    [Fact]
    public async Task ShouldHoldProtectionUntilExactSettlementGivenRejectedPreparation()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, challenge) = await ClaimAsync(host);
        await host.PrepareProcessAsync(account);
        var record = await RunUntilAsync(host, challenge, IdentityRecoveryReceivers.ProtectRecoveryIdentity);
        Assert.IsType<ReceiverOutcome.Applied>((await host.SendAsync(await host.CommandAsync(record,
            IdentityRecoveryReceivers.ProtectRecoveryIdentity, NewIdentity.ToString()))).Value);
        var other = Uuid.CreateVersion4();
        var heldRegistration = await TryRegisterAsync(host, other);
        await host.SeedAsync(new UserIdentity(account.OldIdentityId),
            identity => identity.Revoke(Uuid.CreateVersion4(), DateTimeOffset.UtcNow));

        // Act
        var heldBeforeSettlement = (await host.IdentityAsync(NewIdentity, challenge)).IsProtectedFor(challenge);
        var driven = await host.DriveAsync(account);
        var releasedRegistration = await TryRegisterAsync(host, other);

        // Assert
        Assert.False(heldRegistration.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, heldRegistration.Error.Kind);
        Assert.True(heldBeforeSettlement);
        Assert.Equal(ProcessState.Aborted, driven.State);
        Assert.False((await host.IdentityAsync(NewIdentity, challenge)).IsProtectedFor(challenge));
        Assert.True(releasedRegistration.IsSuccess);
        Assert.Equal(other, (await host.IdentityAsync(NewIdentity, challenge)).UserId);
    }

    [Fact]
    public async Task ShouldKeepReplacementProtectedUntilSettleStageGivenAcceptedSwap()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, challenge) = await ClaimAsync(host);
        await host.PrepareProcessAsync(account);

        // Act
        var record = await RunUntilAsync(host, challenge, IdentityRecoveryReceivers.RegisterRecoveredIdentity);
        var heldRegistration = await TryRegisterAsync(host, Uuid.CreateVersion4());
        var driven = await host.ResumeAsync(challenge);

        // Assert
        Assert.Equal(ProcessState.Pending, record.State);
        Assert.False(heldRegistration.IsSuccess);
        Assert.Equal(UserIdentity.ProtectedForRecovery, heldRegistration.Error);
        Assert.Equal(ProcessState.Completed, driven.State);
        Assert.Equal(account.UserId, (await host.IdentityAsync(NewIdentity, challenge)).UserId);
    }

    [Fact]
    public async Task ShouldWaitGivenAnotherRecoveryOfTheSameAccountHoldsTheSlot()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, first) = await ClaimAsync(host);
        await host.PrepareProcessAsync(account);
        var record = await RunUntilAsync(host, first, IdentityRecoveryReceivers.ProtectRecoveryIdentity);
        Assert.IsType<ReceiverOutcome.Applied>((await host.SendAsync(await host.CommandAsync(record,
            IdentityRecoveryReceivers.ProtectRecoveryIdentity, account.OldIdentityId.ToString()))).Value);
        host.Clock.Advance(TimeSpan.FromMinutes(16));
        var second = Uuid.CreateVersion4();
        var token = await host.StartedAsync(account, second);
        Assert.True((await host.CompleteAsync(account, second, token, subject: "second-subject")).IsSuccess);

        // Act
        var driven = await host.DriveAsync(account, "second", second);

        // Assert
        Assert.Equal(ProcessState.Pending, driven.State);
        Assert.Contains("holds the slot", driven.Note, StringComparison.Ordinal);
        Assert.Equal(IdentityRecovery.Stage.Prepared, (await host.RecoveryAsync(second)).State);
        Assert.False((await host.IdentityAsync(account.OldIdentityId, second)).IsProtectedFor(second));
        Assert.True((await host.IdentityAsync(account.OldIdentityId, first)).IsProtectedFor(first));
    }

    [Fact]
    public async Task ShouldSendNoticeOnceAndKeepRecoveryCompletedGivenRepeatedDeliveryFailure()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, challenge) = await ClaimAsync(host);
        host.Delivery.Failures = 2;

        // Act
        var first = await host.DriveAsync(account);
        var failedNotices = host.Delivery.Notices.Count;
        var stillFailing = await host.ResumeAsync(challenge);
        var resumed = await host.ResumeAsync(challenge);

        // Assert
        Assert.Equal(ProcessState.Pending, first.State);
        Assert.Contains(IdentityRecoveryReceivers.SendIdentityRecoveryNotice, first.Note, StringComparison.Ordinal);
        Assert.Equal(0, failedNotices);
        Assert.Equal(ProcessState.Pending, stillFailing.State);
        Assert.Equal(IdentityRecovery.Stage.Completed, (await host.RecoveryAsync(challenge)).State);
        Assert.True((await host.AddressAsync()).IsRecoveryChallengeCompleted(challenge));
        Assert.True((await host.IdentityAsync(account.OldIdentityId, challenge)).IsRevoked);
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Single(host.Delivery.Notices);
    }

    [Fact]
    public async Task ShouldKeepNewerChallengeSlotGivenOldChallengeCompletes()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, first) = await ClaimAsync(host);
        await host.DriveAsync(account);
        var second = Uuid.CreateVersion4();

        // Act
        var started = await host.StartAsync(second, account.Email);
        var address = await host.AddressAsync(selectedChallengeId: first);

        // Assert
        Assert.True(started.IsSuccess);
        Assert.Equal(second, address.CurrentRecoveryChallengeId);
        Assert.True(address.IsRecoveryChallengeCompleted(first));
        Assert.False(address.IsRecoveryCompleted);
    }

    [Fact]
    public async Task ShouldIgnoreOldCallbackGivenNewerAttempt()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, first) = await ClaimAsync(host);
        var driven = await host.DriveAsync(account);
        var firstRecord = (await host.ProcessAsync(first))!;
        var secondChallenge = Uuid.CreateVersion4();
        await host.StartedAsync(account, secondChallenge);

        // Act
        var replayedOldEmailCompletion = await host.SendAsync(await host.CommandAsync(firstRecord,
            IdentityRecoveryReceivers.CompleteEmailRecoveryChallenge, firstRecord.Targets[0]));
        var address = await host.AddressAsync(selectedChallengeId: first);

        // Assert
        Assert.Equal(ProcessState.Completed, driven.State);
        Assert.IsType<ReceiverOutcome.Replayed>(replayedOldEmailCompletion.Value);
        Assert.Equal(secondChallenge, address.CurrentRecoveryChallengeId);
        Assert.False(address.IsRecoveryCompleted);
        Assert.True(address.IsRecoveryChallengeCompleted(first));
        Assert.Single((await host.FactsAsync(new EmailAddress(account.Email).Stream))
            .OfType<IdentityRecoveryChallengeCompleted>());
    }

    [Fact]
    public async Task ShouldReturnReplayedGivenRepeatedReceiverCommand()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, challenge) = await ClaimAsync(host);
        var record = await host.PrepareProcessAsync(account);
        var materialize = await host.CommandAsync(record, IdentityRecoveryReceivers.MaterializeIdentityRecoveryPreparation);

        // Act
        var applied = await host.SendAsync(materialize);
        var replayed = await host.SendAsync(materialize);
        var driven = await host.ResumeAsync(challenge);

        // Assert
        var fact = Assert.IsType<ReceiverOutcome.Applied>(applied.Value).Fact;
        Assert.Equal(fact, Assert.IsType<ReceiverOutcome.Replayed>(replayed.Value).Fact);
        Assert.Equal(ProcessState.Completed, driven.State);
        Assert.Single((await host.FactsAsync(new IdentityRecovery(challenge).Stream)).OfType<IdentityRecoveryPrepared>());
        Assert.Equal(account.UserId, (await host.IdentityAsync(NewIdentity, challenge)).UserId);
    }

    [Fact]
    public async Task ShouldConflictGivenChangedIntentForSameEffectKey()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, _) = await ClaimAsync(host);
        var record = await host.PrepareProcessAsync(account);
        var materialize = (MaterializeIdentityRecoveryPreparation)await host.CommandAsync(record,
            IdentityRecoveryReceivers.MaterializeIdentityRecoveryPreparation);
        Assert.IsType<ReceiverOutcome.Applied>((await host.SendAsync(materialize)).Value);

        // Act
        var changed = await host.SendAsync(materialize with { ExpectedRevision = 7 });

        // Assert
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error.Kind);
        Assert.False(changed.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRefuseReceiverCommandGivenNonSystemPrincipal()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, _) = await ClaimAsync(host);
        var record = await host.PrepareProcessAsync(account);
        var materialize = await host.CommandAsync(record, IdentityRecoveryReceivers.MaterializeIdentityRecoveryPreparation);

        // Act
        var refused = await host.SendAsync(materialize, IdentityRecoveryHost.ProviderActor("https://x/", "someone"));

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, refused.Error.Kind);
        Assert.Empty(await host.FactsAsync(new IdentityRecovery(record.Key.RootOperationId).Stream));
    }

    [Fact]
    public async Task ShouldRefuseAcceptanceGivenMissingOrMismatchedProtectionProofs()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, challenge) = await ClaimAsync(host);
        var record = await host.PrepareProcessAsync(account);
        await RunUntilAsync(host, challenge, IdentityRecoveryReceivers.AcceptProtectedIdentityRecovery);
        record = (await host.ProcessAsync(challenge))!;
        var accept = (AcceptProtectedIdentityRecovery)await host.CommandAsync(record,
            IdentityRecoveryReceivers.AcceptProtectedIdentityRecovery);
        var swapped = await host.Content.ReadJsonAsync<IdentityRecoveryProtectionProofs>("bdgrz", accept.ProtectionProofs);

        // Act
        var wrongContent = await host.Content.WriteJsonAsync("bdgrz", Uuid.CreateVersion4(),
            IdentityRecoveryProtectionProofs.Schema, new IdentityRecoveryProtectionProofs(swapped.Replacement, swapped.Old));
        var refused = await host.SendAsync(accept with { ProtectionProofs = wrongContent });

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.False(refused.Error.IsTransient);
        Assert.DoesNotContain(await host.FactsAsync(new IdentityRecovery(challenge).Stream),
            fact => fact is IdentityRecoveryAccepted);
    }

    [Fact]
    public async Task ShouldRefuseRejectionGivenAcceptedSwapOrUnprovableConflict()
    {
        // Arrange
        await using var host = new IdentityRecoveryHost();
        var (account, challenge) = await ClaimAsync(host);
        await host.PrepareProcessAsync(account);
        await RunUntilAsync(host, challenge, IdentityRecoveryReceivers.RegisterRecoveredIdentity);
        var record = (await host.ProcessAsync(challenge))!;
        var conflict = await host.Content.WriteJsonAsync("bdgrz", Uuid.CreateVersion4(), IdentityRecoveryConflictProof.Schema,
            new IdentityRecoveryConflictProof(NewIdentity, RecoveryIdentityRole.Replacement, "forged"));

        // Act
        var refused = await host.SendAsync(new RejectIdentityRecoveryPreparation(Uuid.CreateVersion4(), challenge,
            IdentityRecovery.FrozenGeneration, conflict));

        // Assert
        Assert.Equal(ProcessState.Pending, record.State);
        Assert.False(refused.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, refused.Error.Kind);
        Assert.DoesNotContain(await host.FactsAsync(new IdentityRecovery(challenge).Stream),
            fact => fact is IdentityRecoveryPreparationRejected);
    }

    [Theory]
    [MemberData(nameof(CrashPoints))]
    public async Task ShouldResumeToTheSameOutcomeGivenCrashAtEveryProcessCommitBoundary(int successfulReplacesBeforeCrash)
    {
        // Arrange
        var crash = new CrashOnceStore();
        await using var host = new IdentityRecoveryHost(crash.Wrap);
        var (account, challenge) = await ClaimAsync(host);
        crash.ArmAfterReplaceCount(successfulReplacesBeforeCrash);

        // Act
        try
        {
            await host.DriveAsync(account);
        }
        catch (InvalidOperationException exception) when (exception.Message == "simulated crash")
        {
            // The worker died between a receiver append and the record CAS; another worker resumes it.
        }

        crash.Disarm();
        var resumed = await host.ResumeAsync(challenge, "second-worker");

        // Assert
        Assert.Equal(ProcessState.Completed, resumed.State);
        AssertExactlyOnce(await host.FactsAsync(new IdentityRecovery(challenge).Stream),
            "IdentityRecoveryPrepared", "IdentityRecoveryAccepted", "IdentityRecoveryCompleted");
        AssertExactlyOnce(await host.FactsAsync(new UserIdentity(account.OldIdentityId).Stream),
            "UserIdentityRegistered", "RecoveryIdentityProtected", "UserIdentityRevoked", "RecoveryIdentityProtectionSettled");
        AssertExactlyOnce((await host.FactsAsync(new UserIdentity(NewIdentity).Stream)),
            "RecoveryIdentityProtected", "UserIdentityRegistered", "RecoveryIdentityProtectionSettled");
        Assert.Single((await host.FactsAsync(new EmailAddress(account.Email).Stream))
            .OfType<IdentityRecoveryChallengeCompleted>());
        Assert.True(host.Delivery.Notices.Count is >= 1 and <= 2);
    }

    [Theory]
    [MemberData(nameof(CrashPoints))]
    public async Task ShouldResumeRejectionToTheSameOutcomeGivenCrashAtEveryProcessCommitBoundary(
        int successfulReplacesBeforeCrash)
    {
        // Arrange
        var crash = new CrashOnceStore();
        await using var host = new IdentityRecoveryHost(crash.Wrap);
        var (account, challenge) = await ClaimAsync(host);
        await host.RegisterElsewhereAsync(IdentityRecoveryHost.NewProvider, NewSubject);
        crash.ArmAfterReplaceCount(successfulReplacesBeforeCrash);

        // Act
        try
        {
            await host.DriveAsync(account);
        }
        catch (InvalidOperationException exception) when (exception.Message == "simulated crash")
        {
            // The worker died between a receiver append and the record CAS; another worker resumes it.
        }

        crash.Disarm();
        var resumed = await host.ResumeAsync(challenge, "second-worker");

        // Assert
        Assert.Equal(ProcessState.Aborted, resumed.State);
        AssertExactlyOnce(await host.FactsAsync(new IdentityRecovery(challenge).Stream),
            "IdentityRecoveryPrepared", "IdentityRecoveryPreparationRejected");
        Assert.DoesNotContain(await host.FactsAsync(new IdentityRecovery(challenge).Stream),
            fact => fact is IdentityRecoveryAccepted);
        Assert.True((await host.IdentityAsync(account.OldIdentityId, challenge)).IsSettledFor(challenge));
        Assert.True((await host.IdentityAsync(NewIdentity, challenge)).IsSettledFor(challenge));
        Assert.False((await host.IdentityAsync(account.OldIdentityId, challenge)).IsRevoked);
        Assert.Empty(host.Delivery.Notices);
    }

    const int CrashPointCount = 34;

    public static IEnumerable<object[]> CrashPoints() =>
        Enumerable.Range(0, CrashPointCount).Select(count => new object[] { count });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldCoverEveryCommitBoundaryGivenTheCrashPointRange(bool rejected)
    {
        // Arrange
        var crash = new CrashOnceStore();
        await using var host = new IdentityRecoveryHost(crash.Wrap);
        var (account, _) = await ClaimAsync(host);
        if (rejected)
            await host.RegisterElsewhereAsync(IdentityRecoveryHost.NewProvider, NewSubject);

        // Act
        var record = await host.DriveAsync(account);

        // Assert
        Assert.True(record.IsTerminal);
        Assert.InRange(crash.Replaces, 1, CrashPointCount - 1);
    }

    [Theory]
    [MemberData(nameof(CrashPoints))]
    public async Task ShouldCrashAtTheArmedBoundaryGivenCrashPoint(int successfulReplacesBeforeCrash)
    {
        // Arrange
        var crash = new CrashOnceStore();
        await using var host = new IdentityRecoveryHost(crash.Wrap);
        var (account, _) = await ClaimAsync(host);
        crash.ArmAfterReplaceCount(successfulReplacesBeforeCrash);

        // Act
        var outcome = await Record.ExceptionAsync(() => host.DriveAsync(account));

        // Assert
        Assert.Equal(crash.Triggered, outcome is InvalidOperationException);
        Assert.True(crash.Triggered || crash.Replaces <= successfulReplacesBeforeCrash);
    }

    static OperationProvenance EmailAddressFactsOperation(EventStreamAddress owner, Uuid challenge) =>
        new(Uuid.CreateVersion4(), challenge, null, "RejectIdentityRecoveryPreparation",
            new Bdgrz.Compliance.Features.AccessControl.Models.ActorReference("user", "tester", "tester"), owner,
            challenge.ToString(), null, new string('b', 64), DateTimeOffset.UtcNow, owner, owner);

    static void AssertExactlyOnce(IEnumerable<DomainEvent> facts, params string[] expected)
    {
        var names = facts.Select(fact => fact.GetType().Name).ToArray();
        foreach (var name in expected)
            Assert.True(names.Count(candidate => candidate == name) == 1, $"{name} was appended {names.Count(candidate => candidate == name)} times.");
    }

    static async Task<(IdentityRecoveryHost.Account Account, Uuid Challenge)> ClaimAsync(IdentityRecoveryHost host,
        string? assertedEmail = null)
    {
        var account = await host.SeedAccountAsync();
        var challenge = Uuid.CreateVersion4();
        var token = await host.StartedAsync(account, challenge);
        var claimed = await host.CompleteAsync(account, challenge, token, subject: NewSubject, assertedEmail: assertedEmail);
        Assert.True(claimed.IsSuccess, claimed.Error?.Message);
        return (account, challenge);
    }

    /// <summary>Drives the record step by step until the named receiver is the next step, without running it.</summary>
    static async Task<ProcessRecord> RunUntilAsync(IdentityRecoveryHost host, Uuid challenge, string receiver)
    {
        var definition = IdentityRecoveryProcess.Definition;
        var record = (await host.ProcessAsync(challenge))!;
        while (true)
        {
            var step = ProcessDriver.NextStep(definition, record)
                ?? throw new InvalidOperationException($"The process finished before {receiver}.");
            if (step.Receiver == receiver)
                return record;
            var command = await definition.CreateReceiver(step, host.Provider, CancellationToken.None);
            NativeFactReference? produced = null;
            if (command is not null)
            {
                var outcome = await host.SendAsync(command);
                Assert.True(outcome.IsSuccess, outcome.Error?.Message);
                produced = outcome.Value switch
                {
                    ReceiverOutcome.Applied applied => applied.Fact,
                    ReceiverOutcome.Replayed replayed => replayed.Fact,
                    _ => null,
                };
            }

            record = await Advance(host, record, step, definition, produced);
        }
    }

    static async Task<ProcessRecord> Advance(IdentityRecoveryHost host, ProcessRecord record, ProcessStep step,
        ProcessDefinition definition, NativeFactReference? produced)
    {
        var stageIndex = definition.Stages.ToList().FindIndex(stage => stage.Stage == step.Stage);
        var stage = definition.Stages[stageIndex];
        var receiverIndex = stage.Receivers.ToList().IndexOf(step.Receiver);
        var targetIndex = step.Target is null ? record.Targets.Count : record.Targets.ToList().IndexOf(step.Target) + 1;
        var next = stage.IsPerTarget && targetIndex < record.Targets.Count
            ? record with { StageIndex = stageIndex, ReceiverIndex = receiverIndex, TargetIndex = targetIndex }
            : receiverIndex + 1 < stage.Receivers.Count
                ? record with { StageIndex = stageIndex, ReceiverIndex = receiverIndex + 1, TargetIndex = 0 }
                : record with { StageIndex = stageIndex + 1, ReceiverIndex = 0, TargetIndex = 0 };
        if (produced is not null)
            next = next with
            {
                Facts = new Dictionary<string, NativeFactReference>(next.Facts)
                {
                    [ProcessRecord.FactKey(step.Receiver, step.Target)] = produced,
                },
            };
        return await host.Store.ReplaceAsync(next, record.Version);
    }

    static async Task<Result<AuthenticatedUserIdentity>> TryRegisterAsync(IdentityRecoveryHost host, Uuid otherUser)
    {
        var executor = host.Provider.GetRequiredService<IAggregateExecutor>();
        return await executor.ExecuteAsync(new UserIdentity(IdentityRecoveryHost.NewProvider, NewSubject),
            identity => AggregateOutcome.CommitOnSuccess(identity.Register(otherUser, null)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }

    /// <summary>Decorates the store so one ReplaceAsync, chosen by count, fails after the receiver already ran.</summary>
    sealed class CrashOnceStore
    {
        int _remaining = -1;

        public int Replaces { get; private set; }

        public bool Triggered { get; private set; }

        public void ArmAfterReplaceCount(int successfulReplacesBeforeCrash) => _remaining = successfulReplacesBeforeCrash;

        public void Disarm() => _remaining = -1;

        public IProcessRecordStore Wrap(IProcessRecordStore inner) => new Decorator(inner, this);

        sealed class Decorator(IProcessRecordStore inner, CrashOnceStore owner) : IProcessRecordStore
        {
            public Task<ProcessRecord?> ReadAsync(ProcessKey key, CancellationToken ct = default) =>
                inner.ReadAsync(key, ct);

            public Task<ProcessRecord> CreateIfAbsentAsync(ProcessRecord record, CancellationToken ct = default) =>
                inner.CreateIfAbsentAsync(record, ct);

            public async Task<ProcessRecord> ReplaceAsync(ProcessRecord record, long expectedVersion,
                CancellationToken ct = default)
            {
                owner.Replaces++;
                if (owner._remaining == 0)
                {
                    owner._remaining = -1;
                    owner.Triggered = true;
                    throw new InvalidOperationException("simulated crash");
                }

                if (owner._remaining > 0)
                    owner._remaining--;
                return await inner.ReplaceAsync(record, expectedVersion, ct).ConfigureAwait(false);
            }

            public Task<IReadOnlyList<ProcessKey>> ScanPendingAsync(string processKind, DateTimeOffset dueBefore,
                int limit, CancellationToken ct = default) => inner.ScanPendingAsync(processKind, dueBefore, limit, ct);
        }
    }
}
