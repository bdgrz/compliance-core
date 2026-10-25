using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Builds the typed receiver commands of the identity recovery process. Every command id is the deterministic
///     step id the process driver derived from the root source fact, the receiver, the target and the challenge,
///     so a replayed step sends the identical command. Proofs are built from the committed facts the process record
///     references, never from caller input.
/// </summary>
static class IdentityRecoveryEffects
{
    /// <summary>Builds the receiver command of the step, or null when the step does not apply to the record.</summary>
    public static async ValueTask<IRequest<ReceiverOutcome>?> CreateAsync(ProcessStep step, IdentityRecoveryIntent intent,
        ISourceFactResolver resolver, IImmutableContentStore content, IAggregateReader reader, IDomainEventReader events, CancellationToken ct)
    {
        var realm = step.Record.Key.Realm;
        var challenge = intent.ChallengeId;
        var recovery = new IdentityRecovery(challenge).Stream;
        var aborting = step.Record.State == ProcessState.Aborting;
        var frozen = step.Record.Intent;
        const long generation = IdentityRecovery.FrozenGeneration;
        switch (step.Receiver)
        {
            case IdentityRecoveryReceivers.MaterializeIdentityRecoveryPreparation:
                {
                    var (proof, _, _) = await IdentityRecoverySourceFacts.BuildAsync<IdentityRecoveryChallengeClaimed>(resolver,
                        content, events, realm, step.Record.Source, ct).ConfigureAwait(false);
                    return new MaterializeIdentityRecoveryPreparation(step.CommandId, proof, recovery, challenge,
                        generation, null, frozen, challenge, intent.AccountId, intent.OldIdentityId,
                        intent.ReplacementIdentityId, intent.EmailProofProvenance);
                }
            case IdentityRecoveryReceivers.ProtectRecoveryIdentity:
                {
                    var identity = IdentityRecoveryReceiverSupport.TargetIdentity(step.Target
                        ?? throw new InvalidOperationException("Protection dispatches once per identity slot."));
                    var role = identity == intent.OldIdentityId ? RecoveryIdentityRole.Old : RecoveryIdentityRole.Replacement;
                    return new ProtectRecoveryIdentity(step.CommandId, await PreparedAsync(step, resolver, content, events, ct)
                            .ConfigureAwait(false), IdentityRecoveryReceiverSupport.IdentityStream(identity), challenge,
                        generation, null, frozen, challenge, intent.AccountId, intent.OldIdentityId,
                        intent.ReplacementIdentityId, identity, role);
                }
            case IdentityRecoveryReceivers.AcceptProtectedIdentityRecovery:
                return new AcceptProtectedIdentityRecovery(step.CommandId, await PreparedAsync(step, resolver, content, events, ct)
                        .ConfigureAwait(false), recovery, challenge, generation, null, frozen, challenge,
                    await ProtectionProofsAsync(step, intent, resolver, content, events, ct).ConfigureAwait(false));
            case IdentityRecoveryReceivers.RegisterRecoveredIdentity:
                return new RegisterRecoveredIdentity(step.CommandId, await AcceptedAsync(step, resolver, content, events, ct)
                        .ConfigureAwait(false), IdentityRecoveryReceiverSupport.IdentityStream(intent.ReplacementIdentityId),
                    challenge, generation, null, frozen, challenge, intent.AccountId, intent.ReplacementIdentityId,
                    await ProtectionProofsAsync(step, intent, resolver, content, events, ct).ConfigureAwait(false));
            case IdentityRecoveryReceivers.RevokeRecoveredOldIdentity:
                return new RevokeRecoveredOldIdentity(step.CommandId, await AcceptedAsync(step, resolver, content, events, ct)
                        .ConfigureAwait(false), IdentityRecoveryReceiverSupport.IdentityStream(intent.OldIdentityId),
                    challenge, generation, null, frozen, challenge, intent.OldIdentityId, intent.ReplacementIdentityId,
                    await RegisteredAsync(step, resolver, content, events, ct).ConfigureAwait(false));
            case IdentityRecoveryReceivers.CompleteAcceptedIdentityRecovery:
                return new CompleteAcceptedIdentityRecovery(step.CommandId, await AcceptedAsync(step, resolver, content, events, ct)
                        .ConfigureAwait(false), recovery, challenge, generation, null, frozen, challenge,
                    intent.AccountId, await RegisteredAsync(step, resolver, content, events, ct).ConfigureAwait(false),
                    (await IdentityRecoverySourceFacts.BuildAsync<UserIdentityRevoked>(resolver, content, events, realm,
                        Fact(step, IdentityRecoveryReceivers.RevokeRecoveredOldIdentity), ct).ConfigureAwait(false)).Proof);
            case IdentityRecoveryReceivers.SettleRecoveryIdentityProtection:
                {
                    var identity = IdentityRecoveryReceiverSupport.TargetIdentity(step.Target
                        ?? throw new InvalidOperationException("Settlement dispatches once per identity slot."));
                    SettleRecoveryIdentityProtectionSourceProof source = aborting
                        ? new SettleRecoveryIdentityProtectionSourceProof.RejectedProof(
                            (await IdentityRecoverySourceFacts.BuildAsync<IdentityRecoveryPreparationRejected>(resolver, content, events,
                                realm, Fact(step, IdentityRecoveryReceivers.RejectIdentityRecoveryPreparation), ct)
                                .ConfigureAwait(false)).Proof)
                        : new SettleRecoveryIdentityProtectionSourceProof.CompletedProof(
                            await CompletedAsync(step, resolver, content, events, ct).ConfigureAwait(false));
                    return new SettleRecoveryIdentityProtection(step.CommandId, source,
                        IdentityRecoveryReceiverSupport.IdentityStream(identity), challenge, generation, null, frozen,
                        challenge, identity);
                }
            case IdentityRecoveryReceivers.CompleteEmailRecoveryChallenge:
                return aborting || !IsFirstTarget(step)
                    ? null
                    : new CompleteEmailRecoveryChallenge(step.CommandId,
                        await CompletedAsync(step, resolver, content, events, ct).ConfigureAwait(false),
                        step.Record.Source.Stream, challenge, generation, null, frozen, challenge);
            case IdentityRecoveryReceivers.SendIdentityRecoveryNotice:
                {
                    if (aborting || !IsFirstTarget(step))
                        return null;
                    var (_, claimed, _) = await IdentityRecoverySourceFacts.BuildAsync<IdentityRecoveryChallengeClaimed>(resolver,
                        content, events, realm, step.Record.Source, ct).ConfigureAwait(false);
                    return new SendIdentityRecoveryNotice(step.CommandId,
                        await CompletedAsync(step, resolver, content, events, ct).ConfigureAwait(false),
                        IdentityRecoveryReceiverSupport.NoticeStream(challenge), challenge, generation, null, frozen,
                        challenge, claimed.EmailAddress);
                }
            case IdentityRecoveryReceivers.RejectIdentityRecoveryPreparation:
                {
                    var conflict = await ConflictAsync(step, intent, content, reader, ct).ConfigureAwait(false);
                    return new RejectIdentityRecoveryPreparation(step.CommandId, challenge, generation, conflict);
                }
            default:
                return null;
        }
    }

    /// <summary>The settle receivers other than the per-slot release dispatch once, with the first canonical slot.</summary>
    static bool IsFirstTarget(ProcessStep step) =>
        step.Record.Targets.Count == 0 || step.Target == step.Record.Targets[0];

    static NativeFactReference Fact(ProcessStep step, string receiver, string? target = null) =>
        step.Record.Facts.TryGetValue(ProcessRecord.FactKey(receiver, target), out var fact)
            ? fact
            : throw new InvalidOperationException($"{receiver} committed no fact to build a proof from.");

    static async Task<SourceFactProof<IdentityRecoveryPrepared>> PreparedAsync(ProcessStep step,
        ISourceFactResolver resolver, IImmutableContentStore content, IDomainEventReader events, CancellationToken ct) =>
        (await IdentityRecoverySourceFacts.BuildAsync<IdentityRecoveryPrepared>(resolver, content, events, step.Record.Key.Realm,
            Fact(step, IdentityRecoveryReceivers.MaterializeIdentityRecoveryPreparation), ct).ConfigureAwait(false)).Proof;

    static async Task<SourceFactProof<IdentityRecoveryAccepted>> AcceptedAsync(ProcessStep step,
        ISourceFactResolver resolver, IImmutableContentStore content, IDomainEventReader events, CancellationToken ct) =>
        (await IdentityRecoverySourceFacts.BuildAsync<IdentityRecoveryAccepted>(resolver, content, events, step.Record.Key.Realm,
            Fact(step, IdentityRecoveryReceivers.AcceptProtectedIdentityRecovery), ct).ConfigureAwait(false)).Proof;

    static async Task<SourceFactProof<UserIdentityRegistered>> RegisteredAsync(ProcessStep step,
        ISourceFactResolver resolver, IImmutableContentStore content, IDomainEventReader events, CancellationToken ct) =>
        (await IdentityRecoverySourceFacts.BuildAsync<UserIdentityRegistered>(resolver, content, events, step.Record.Key.Realm,
            Fact(step, IdentityRecoveryReceivers.RegisterRecoveredIdentity), ct).ConfigureAwait(false)).Proof;

    static async Task<SourceFactProof<IdentityRecoveryCompleted>> CompletedAsync(ProcessStep step,
        ISourceFactResolver resolver, IImmutableContentStore content, IDomainEventReader events, CancellationToken ct) =>
        (await IdentityRecoverySourceFacts.BuildAsync<IdentityRecoveryCompleted>(resolver, content, events, step.Record.Key.Realm,
            Fact(step, IdentityRecoveryReceivers.CompleteAcceptedIdentityRecovery), ct).ConfigureAwait(false)).Proof;

    /// <summary>Writes the exact protection proofs of both slots; the content id is derived from the challenge, so every receiver names the same reference.</summary>
    static async Task<ImmutableContentRef> ProtectionProofsAsync(ProcessStep step, IdentityRecoveryIntent intent,
        ISourceFactResolver resolver, IImmutableContentStore content, IDomainEventReader events, CancellationToken ct)
    {
        var realm = step.Record.Key.Realm;
        var old = await IdentityRecoverySourceFacts.BuildAsync<RecoveryIdentityProtected>(resolver, content, events, realm,
            Fact(step, IdentityRecoveryReceivers.ProtectRecoveryIdentity, intent.OldIdentityId.ToString()), ct)
            .ConfigureAwait(false);
        var replacement = await IdentityRecoverySourceFacts.BuildAsync<RecoveryIdentityProtected>(resolver, content, events, realm,
            Fact(step, IdentityRecoveryReceivers.ProtectRecoveryIdentity, intent.ReplacementIdentityId.ToString()), ct)
            .ConfigureAwait(false);
        return await content.WriteJsonAsync(realm,
            Uuid.CreateVersion5(intent.ChallengeId, IdentityRecoveryProtectionProofs.Schema),
            IdentityRecoveryProtectionProofs.Schema, new IdentityRecoveryProtectionProofs(old.Proof, replacement.Proof), ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Names the first slot, in canonical order, whose authoritative association conflicts. When none does the
    ///     proof still names the first slot; the rejection handler then refuses it, which quarantines the record
    ///     for repair rather than rejecting a recovery without a provable conflict.
    /// </summary>
    static async Task<ImmutableContentRef> ConflictAsync(ProcessStep step, IdentityRecoveryIntent intent,
        IImmutableContentStore content, IAggregateReader reader, CancellationToken ct)
    {
        IdentityRecoveryConflictProof? proof = null;
        foreach (var target in step.Record.Targets)
        {
            var identity = IdentityRecoveryReceiverSupport.TargetIdentity(target);
            var role = identity == intent.OldIdentityId ? RecoveryIdentityRole.Old : RecoveryIdentityRole.Replacement;
            var slot = await reader.HydrateAsync(new UserIdentity(identity, intent.ChallengeId), ct).ConfigureAwait(false);
            var decision = slot.DecideProtection(intent.ChallengeId, intent.AccountId, role, intent.ReplacementIdentityId);
            proof ??= new IdentityRecoveryConflictProof(identity, role, decision.Reason);
            if (decision.Outcome == UserIdentity.ProtectionOutcome.Conflict)
            {
                proof = new IdentityRecoveryConflictProof(identity, role, decision.Reason);
                break;
            }
        }

        return await content.WriteJsonAsync(step.Record.Key.Realm,
            Uuid.CreateVersion4(), IdentityRecoveryConflictProof.Schema,
            proof ?? throw new InvalidOperationException("The record has no identity slots."), ct).ConfigureAwait(false);
    }
}
