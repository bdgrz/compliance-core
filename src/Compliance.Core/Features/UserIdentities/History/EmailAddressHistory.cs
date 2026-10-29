using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Projections;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Events;
namespace Bdgrz.Compliance.Features.UserIdentities.History;

/// <summary>Composes every accepted claim and completion while the live address retains current/selected authority only.</summary>
public static class EmailAddressHistory
{
    public static async Task<EmailAddressHistorySnapshot> ReadAsync(IDomainEventReader events, EmailAddress owner,
        CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var claims = new Dictionary<Uuid, EmailAddress.RecoveryClaim>();
        var completed = new HashSet<Uuid>();
        var logical = new HashSet<(Uuid Id, string Kind)>();
        var digest = IdentityOwnerReplayCut.Empty;
        var metadataDigest = IdentityOwnerReplayCut.Empty;
        var knownPrefix = IdentityOwnerReplayCut.Empty;
        Uuid? userId = null;
        EmailChallengeIssued? issued = null;
        IdentityRecoveryChallengeIssued? recovery = null;
        var verified = false;
        foreach (var fact in facts)
        {
            if (fact is not INativeOperationFact native || native.Operation.Owner != owner.Stream ||
                native.Operation.LogicalId == Uuid.Empty || !logical.Add((native.Operation.LogicalId, native.Operation.CommandKind)))
                throw Invalid("The address history contains another owner, unsupported native fact or repeated logical receipt.");
            var operation = native.Operation;
            digest = IdentityOwnerReplayCut.Append(digest, fact);
            metadataDigest = IdentityOwnerReplayCut.Append(metadataDigest, fact, true);
            if (fact.Metadata.AggregateVersion <= owner.ReplayCut.KnownThroughVersion)
                knownPrefix = IdentityOwnerReplayCut.Append(knownPrefix, fact, true);
            if (operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                (owner.Acceptance.SelectedCommandKind is null || operation.CommandKind == owner.Acceptance.SelectedCommandKind) &&
                (owner.Acceptance.SelectedFact is not { } selected || selected.GetType() != fact.GetType() ||
                    !selected.Equals(fact) || selected.Metadata != fact.Metadata || owner.Acceptance.SelectedProvenance != operation))
                throw Invalid("The selected address receipt differs from its actual full payload and metadata.");
            var address = fact switch
            {
                EmailAddressReserved value => value.EmailAddress,
                EmailChallengeIssued value => value.EmailAddress,
                EmailAddressVerified value => value.EmailAddress,
                IdentityRecoveryChallengeIssued value => value.EmailAddress,
                IdentityRecoveryChallengeClaimed value => value.EmailAddress,
                IdentityRecoveryChallengeCompleted value => value.EmailAddress,
                _ => throw Invalid("The address history contains an unsupported native fact."),
            };
            if (!EmailAddresses.TryNormalize(address, out var normalized) || normalized != address ||
                new EmailAddress(normalized).Id != owner.Id || owner.Address != address)
                throw Invalid("The address history differs from its normalized identity and exact stream.");
            switch (fact)
            {
                case EmailAddressReserved value:
                    if (userId is not null || value.UserId == Uuid.Empty || operation.CommandKind != nameof(ReserveEmail))
                        throw Invalid("An address can have one nonempty initial reservation.");
                    userId = value.UserId;
                    break;
                case EmailChallengeIssued value:
                    if (userId != value.UserId || verified || value.ChallengeId == Uuid.Empty ||
                        value.TokenHash.Length != 64 || !value.TokenHash.All(Uri.IsHexDigit) ||
                        string.IsNullOrWhiteSpace(value.TokenKeyId) || operation.LogicalId != value.ChallengeId ||
                        operation.CommandKind != nameof(IssueEmailChallenge) || value.ChallengeId == recovery?.ChallengeId ||
                        claims.ContainsKey(value.ChallengeId))
                        throw Invalid("A verification challenge must belong to its unverified reserved address.");
                    issued = value;
                    break;
                case EmailAddressVerified value:
                    if (userId != value.UserId || issued?.ChallengeId != value.ChallengeId ||
                        operation.LogicalId != value.ChallengeId || operation.CommandKind != nameof(CompleteEmailChallenge))
                        throw Invalid("The verification must complete the current challenge of its reserved address.");
                    verified = true;
                    break;
                case IdentityRecoveryChallengeIssued value:
                    if (userId != value.UserId || !verified || value.ChallengeId == Uuid.Empty ||
                        value.TokenHash.Length != 64 || !value.TokenHash.All(Uri.IsHexDigit) ||
                        string.IsNullOrWhiteSpace(value.TokenKeyId) || value.ExpiresAt <= DateTimeOffset.MinValue ||
                        value.ChallengeId == issued?.ChallengeId || claims.ContainsKey(value.ChallengeId) ||
                        operation.LogicalId != value.ChallengeId || operation.CommandKind != nameof(StartIdentityRecovery))
                        throw Invalid("A recovery challenge must be a new exact challenge of its verified reserved address.");
                    recovery = value;
                    break;
                case IdentityRecoveryChallengeClaimed value:
                    if (userId != value.UserId || !verified || recovery?.ChallengeId != value.ChallengeId ||
                        value.OldIdentityId == Uuid.Empty || value.ReplacementIdentityId == Uuid.Empty ||
                        value.OldIdentityId == value.ReplacementIdentityId || operation.LogicalId != value.ChallengeId ||
                        operation.RootOperationId != value.ChallengeId || operation.CommandKind != nameof(CompleteIdentityRecovery) ||
                        !claims.TryAdd(value.ChallengeId, new EmailAddress.RecoveryClaim(value.OldIdentityId, value.ReplacementIdentityId, value)))
                        throw Invalid("The recovery claim must freeze its exact current verified challenge once.");
                    break;
                case IdentityRecoveryChallengeCompleted value:
                    if (userId != value.UserId || !claims.TryGetValue(value.ChallengeId, out var claim) ||
                        claim.OldIdentityId != value.OldIdentityId || claim.ReplacementIdentityId != value.ReplacementIdentityId ||
                        claim.Fact.Intent != value.Intent || operation.RootOperationId != value.ChallengeId ||
                        operation.CommandKind != nameof(CompleteEmailRecoveryChallenge) || !completed.Add(value.ChallengeId))
                        throw Invalid("A completion must retain the exact account, candidate and intent of its historical claim once.");
                    break;
                default:
                    throw Invalid("The address history contains an unsupported native fact.");
            }
        }
        var current = recovery is null ? null : claims.GetValueOrDefault(recovery.ChallengeId);
        if (owner.Owner != userId || owner.IsVerified != verified || owner.CurrentIssuedFact != issued ||
            owner.CurrentRecoveryIssuedFact != recovery || owner.CurrentChallengeId != issued?.ChallengeId ||
            owner.CurrentRecoveryChallengeId != recovery?.ChallengeId ||
            owner.RecoveryOldIdentityId != current?.OldIdentityId || owner.RecoveryReplacementIdentityId != current?.ReplacementIdentityId ||
            owner.IsRecoveryCompleted != (recovery is not null && completed.Contains(recovery.ChallengeId)) ||
            owner.SelectedChallengeId != Uuid.Empty && (owner.ClaimOf(owner.SelectedChallengeId) != claims.GetValueOrDefault(owner.SelectedChallengeId) ||
                owner.IsRecoveryChallengeCompleted(owner.SelectedChallengeId) != completed.Contains(owner.SelectedChallengeId)))
            throw Invalid("The complete address prefix differs from its compact current and selected authority.");
        owner.ReplayCut.Confirm(owner, digest, metadataDigest, knownPrefix);
        return new EmailAddressHistorySnapshot(facts, claims, completed);
    }

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
