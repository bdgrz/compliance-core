using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Verifies complete immutable expectation-line history outside compact command authority.</summary>
public static class AccessExpectationHistory
{
    public static async Task<IReadOnlyList<DomainEvent>> ReadAsync(IDomainEventReader events, AccessExpectation owner,
        CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var digest = AccessReviewReplayDigest.Empty;
        var logicalIds = new HashSet<Uuid>();
        var proposed = new Dictionary<Uuid, Uuid>();
        var approved = new HashSet<Uuid>();
        Uuid? system = null;
        long revision = 0;
        foreach (var fact in facts)
        {
            if (fact is not INativeOperationFact native || native.Operation.Owner != owner.Stream ||
                native.Operation.LogicalId == Uuid.Empty || !logicalIds.Add(native.Operation.LogicalId) ||
                native.Operation.Actor is not { Kind: "member" } actor ||
                !Uuid.TryParse(actor.Id, CultureInfo.InvariantCulture, out var actorId) || actorId == Uuid.Empty)
                throw Invalid("The expectation history has another owner or repeated logical operation.");
            digest = AccessReviewReplayDigest.Append(digest, fact);
            if (native.Operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                (owner.Acceptance.SelectedFact is not { } selected || selected.GetType() != fact.GetType() ||
                    !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                throw Invalid("The selected expectation acceptance differs from its actual payload and complete metadata.");
            var operation = native.Operation;
            bool valid;
            switch (fact)
            {
                case AccessExpectationProposed proposal:
                    valid = Scope(proposal.TenantId, proposal.SystemInstanceId, proposal.Revision) &&
                        (revision != 0 || operation.LogicalId == owner.Id) &&
                        proposal.ProposerMemberId == actorId && proposed.TryAdd(operation.LogicalId, proposal.ProposerMemberId) &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(ProposeAccessExpectation), (ulong)revision);
                    system ??= proposal.SystemInstanceId;
                    break;
                case AccessExpectationApproved approval:
                    valid = Scope(approval.TenantId, approval.SystemInstanceId, approval.Revision) &&
                        proposed.TryGetValue(approval.ExpectationId, out var proposerId) && approved.Add(approval.ExpectationId) &&
                        (proposerId != actorId || approval.SeparationOfDutiesWaiverId is { } waiver && waiver != Uuid.Empty) &&
                        approval.ApprovedBy == operation.Actor && approval.ApprovedAt == operation.AcceptedAt &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(ApproveAccessExpectation), (ulong)revision);
                    break;
                case AccessExpectationExceptionRecorded exception:
                    valid = Scope(exception.TenantId, exception.SystemInstanceId, exception.Revision) &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(ExemptAccessExpectation), (ulong)revision);
                    break;
                default:
                    valid = false;
                    break;
            }
            if (!valid)
                throw Invalid("The expectation history has another scope, predecessor, revision or native root binding.");
            revision++;
        }
        if (revision != owner.Revision || system != owner.SystemInstanceId || (facts.Count == 0) != (owner.Latest is null) ||
            facts.Count != 0 && ContentDigest.Sha256(facts[^1], facts[^1].GetType()) !=
                ContentDigest.Sha256(owner.Latest!, owner.Latest!.GetType()))
            throw Invalid("The full expectation history differs from compact current authority.");
        owner.ReplayCut.Confirm(owner, digest);
        ct.ThrowIfCancellationRequested();
        return Array.AsReadOnly(facts.ToArray());

        bool Scope(Uuid tenant, Uuid instance, long next) => tenant == owner.TenantId && instance != Uuid.Empty &&
            (system is null || system == instance) && next == revision + 1;
    }

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
