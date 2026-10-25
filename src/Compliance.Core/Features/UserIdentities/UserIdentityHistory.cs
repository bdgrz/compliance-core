using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Reads complete immutable settlement history outside the identity command owner.</summary>
public static class UserIdentityHistory
{
    public static async Task<UserIdentityHistorySnapshot> ReadAsync(IDomainEventReader events, UserIdentity owner,
        CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var settled = new HashSet<Uuid>();
        var protections = new Dictionary<Uuid, RecoveryIdentityProtected>();
        var logical = new HashSet<(Uuid Id, string Kind)>();
        var digest = IdentityOwnerReplayCut.Empty;
        var metadataDigest = IdentityOwnerReplayCut.Empty;
        var knownPrefix = IdentityOwnerReplayCut.Empty;
        UserIdentityRegistered? registration = null;
        UserIdentityRevoked? revocation = null;
        foreach (var fact in facts)
        {
            digest = IdentityOwnerReplayCut.Append(digest, fact);
            metadataDigest = IdentityOwnerReplayCut.Append(metadataDigest, fact, true);
            if (fact.Metadata.AggregateVersion <= owner.ReplayCut.KnownThroughVersion)
                knownPrefix = IdentityOwnerReplayCut.Append(knownPrefix, fact, true);
            var operation = fact switch
            {
                UserIdentityRegistered value => value.Operation,
                UserIdentityRevoked value => value.Operation,
                INativeOperationFact value => value.Operation,
                _ => throw Invalid("The identity history contains an unsupported native fact."),
            };
            if (operation is not null)
            {
                if (operation.Owner != owner.Stream || operation.LogicalId == Uuid.Empty ||
                    !logical.Add((operation.LogicalId, operation.CommandKind)))
                    throw Invalid("The identity history has another owner or repeated logical receipt.");
                if (operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                    (owner.Acceptance.SelectedCommandKind is null || operation.CommandKind == owner.Acceptance.SelectedCommandKind) &&
                    (owner.Acceptance.SelectedFact is not { } selected || selected.GetType() != fact.GetType() ||
                        !selected.Equals(fact) || selected.Metadata != fact.Metadata ||
                        owner.Acceptance.SelectedProvenance != operation))
                    throw Invalid("The selected identity receipt differs from its actual full payload and metadata.");
            }
            switch (fact)
            {
                case UserIdentityRegistered value:
                    if (registration is not null || value.UserId == Uuid.Empty ||
                        string.IsNullOrWhiteSpace(value.Provider) || string.IsNullOrWhiteSpace(value.Identifier) ||
                        UserIdentity.GetIdentityId(value.Provider, value.Identifier) != owner.Id ||
                        protections.Values.Any(held => held.AccountId != value.UserId))
                        throw Invalid("The identity registration differs from its provider slot or active account protection.");
                    if (value.Operation is not null && (value.RecoveryChallengeId is not { } registeredChallenge ||
                        value.Intent is null || value.Operation.RootOperationId != registeredChallenge ||
                        value.Operation.CommandKind != nameof(RegisterRecoveredIdentity) ||
                        !protections.TryGetValue(registeredChallenge, out var registrationHold) || registrationHold.AccountId != value.UserId ||
                        registrationHold.Role != RecoveryIdentityRole.Replacement || registrationHold.Intent != value.Intent))
                        throw Invalid("A recovered registration must retain its exact active replacement protection and intent.");
                    if (value.Operation is null && (value.RecoveryChallengeId is not null || value.Intent is not null))
                        throw Invalid("A public registration cannot claim recovery provenance without its operation.");
                    registration = value;
                    break;
                case UserIdentityRevoked value:
                    if (registration is null || revocation is not null || value.UserIdentityId != owner.Id ||
                        value.UserId != registration.UserId || value.ReplacementIdentityId == Uuid.Empty ||
                        value.ReplacementIdentityId == owner.Id)
                        throw Invalid("The revocation differs from its registered identity and replacement.");
                    if (value.Operation is not null && (value.RecoveryChallengeId is not { } revokedChallenge ||
                        value.Intent is null || value.Operation.RootOperationId != revokedChallenge ||
                        value.Operation.CommandKind != nameof(RevokeRecoveredOldIdentity) ||
                        !protections.TryGetValue(revokedChallenge, out var revocationHold) || revocationHold.AccountId != value.UserId ||
                        revocationHold.Role != RecoveryIdentityRole.Old || revocationHold.Intent != value.Intent))
                        throw Invalid("A recovered revocation must retain its exact active old protection and intent.");
                    if (value.Operation is null && (protections.Count != 0 || value.RecoveryChallengeId is not null || value.Intent is not null))
                        throw Invalid("A public revocation cannot bypass a held recovery or claim recovery provenance.");
                    revocation = value;
                    break;
                case RecoveryIdentityProtected value:
                    if (value.Owner != owner.Stream || value.ChallengeId == Uuid.Empty || value.AccountId == Uuid.Empty ||
                        !Enum.IsDefined(value.Role) || value.Revision != (long)fact.Metadata.AggregateVersion - 1 ||
                        value.Generation != IdentityRecovery.FrozenGeneration || protections.Count != 0 ||
                        settled.Contains(value.ChallengeId) || value.Operation.RootOperationId != value.ChallengeId ||
                        value.Operation.CommandKind != nameof(ProtectRecoveryIdentity) ||
                        value.Role == RecoveryIdentityRole.Old && (registration is null || registration.UserId != value.AccountId) ||
                        value.Role == RecoveryIdentityRole.Replacement && (revocation is not null ||
                            registration is not null && registration.UserId != value.AccountId) ||
                        !protections.TryAdd(value.ChallengeId, value))
                        throw Invalid("A recovery protection must be a unique, unsettled, compatible current hold of this identity.");
                    break;
                case RecoveryIdentityProtectionSettled value:
                    if (value.Owner != owner.Stream || value.ChallengeId == Uuid.Empty ||
                        value.Revision != (long)fact.Metadata.AggregateVersion - 1 ||
                        value.Generation != IdentityRecovery.FrozenGeneration ||
                        value.Operation.RootOperationId != value.ChallengeId ||
                        value.Operation.CommandKind != nameof(SettleRecoveryIdentityProtection) || !settled.Add(value.ChallengeId))
                        throw Invalid("A recovery settlement must retain the exact unique lifetime fence of this identity.");
                    if (protections.TryGetValue(value.ChallengeId, out var settledHold) && settledHold.Intent != value.Intent)
                        throw Invalid("A settlement cannot release a protection with another frozen intent.");
                    protections.Remove(value.ChallengeId);
                    break;
                default:
                    throw Invalid("The identity history contains an unsupported native fact.");
            }
        }
        var active = protections.Values.SingleOrDefault();
        if (owner.RegistrationFact != registration || owner.RevocationFact != revocation ||
            owner.IsRegistered != (registration is not null) || owner.IsRevoked != (revocation is not null) ||
            owner.UserId != (registration?.UserId ?? Uuid.Empty) ||
            owner.ProtectionChallengeId != active?.ChallengeId ||
            owner.Protection != (active is null ? null : new UserIdentity.RecoveryProtection(active.AccountId, active.Role)) ||
            owner.SelectedChallengeId != Uuid.Empty && owner.IsSettledFor(owner.SelectedChallengeId) != settled.Contains(owner.SelectedChallengeId))
            throw Invalid("The complete identity prefix differs from its compact current and selected authority.");
        if (registration is not null)
        {
            DomainEventRecord? actual = null;
            await foreach (var record in events.ReadAsync(owner.Stream, 0, ct).ConfigureAwait(false))
            {
                if (record.ResourceOffset >= owner.CommittedStreamPosition)
                    break;
                if (record.Event.Metadata.EventId != registration.Metadata.EventId)
                    continue;
                if (record.Stream != owner.Stream || record.Event is not UserIdentityRegistered current ||
                    current != registration || current.Metadata != registration.Metadata)
                    throw Invalid("The physical registration locator differs from its verified native registration.");
                actual = record;
                break;
            }
            if (actual is null)
                throw new ImmutableContentUnavailableException("The actual physical registration record is not visible yet.");
            owner.BindRegistrationProof(registration, actual.ResourceOffset);
        }
        owner.ReplayCut.Confirm(owner, digest, metadataDigest, knownPrefix);
        return new UserIdentityHistorySnapshot(facts, protections, settled);
    }

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
