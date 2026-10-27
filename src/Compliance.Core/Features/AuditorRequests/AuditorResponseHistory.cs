using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Verified immutable response history outside compact command authority, at its exact native/physical cut.</summary>
public static class AuditorResponseHistory
{
    /// <summary>Preserves all original versions/reviews/receipts and verifies current authority and selected use proofs.</summary>
    public static async Task<IReadOnlyList<DomainEvent>> ReadAsync(IDomainEventReader events, AuditorResponse owner,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var uses = new SourceUseHistoryValidation(owner.Stream);
        var operations = new HashSet<Uuid>();
        var status = string.Empty;
        var author = Uuid.Empty;
        long revision = 0;
        long businessVersion = 0;
        long? closedByGeneration = null;
        DomainEvent? latest = null;
        AuditorResponseDelivered? delivery = null;
        AuditorResponseDispositionRecorded? disposition = null;
        foreach (var fact in facts)
        {
            if (fact is not INativeOperationFact native || native.Operation.Owner != owner.Stream ||
                native.Operation.LogicalId == Uuid.Empty || !operations.Add(native.Operation.LogicalId))
                throw Invalid("A response history contains another owner or repeats a logical operation.");
            uses.ObserveOwnerFact(fact);
            if (SourceUseFactKinds.Classify(fact) is { } kind)
            {
                var source = (ISourceUseFact)fact;
                var heldRevision = fact switch
                {
                    AuditorResponseRequestResolutionUseAuthorized granted => granted.Revision,
                    AuditorResponseRequestResolutionUseSettled fence => fence.Revision,
                    _ => -1,
                };
                if (kind.Purpose != SourceUsePurpose.RequestResolution || source.Owner != owner.Stream ||
                    heldRevision != revision || revision == 0 || source.Generation < 1)
                    throw Invalid("A resolution use belongs to the exact materialized content revision.");
                uses.Observe(kind.Purpose, kind.Granted, source);
                if (fact is AuditorResponseRequestResolutionUseSettled { Disposition: SourceUseSettlementDisposition.ParentClosed } settled)
                    closedByGeneration ??= settled.Generation;
                continue;
            }
            var next = revision + 1;
            var operation = native.Operation;
            bool valid;
            switch (fact)
            {
                case AuditResponseDraftCreated created:
                    valid = revision == 0 && Scope(created.TenantId, created.SubjectId) && created.Revision == 1 &&
                        created.ActorMemberId is { } firstAuthor && firstAuthor != Uuid.Empty;
                    author = created.ActorMemberId ?? Uuid.Empty;
                    status = "draft";
                    businessVersion = 1;
                    break;
                case AuditResponseDraftRevised revised:
                    valid = revision > 0 && delivery is null && (status is "draft" or "rejected") &&
                        Scope(revised.TenantId, revised.SubjectId) && revised.Revision == next &&
                        revised.ActorMemberId is { } nextAuthor && nextAuthor != Uuid.Empty &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(ReviseAuditResponseDraft), (ulong)revision);
                    author = revised.ActorMemberId ?? Uuid.Empty;
                    status = "draft";
                    businessVersion++;
                    break;
                case AuditorResponseSubmitted submitted:
                    valid = status == "draft" && Scope(submitted.TenantId, submitted.SubjectId) &&
                        submitted.Revision == next && submitted.Version == businessVersion &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(SubmitAuditResponse), (ulong)revision);
                    status = "submitted";
                    break;
                case AuditorResponseDisclosureReviewed reviewed:
                    valid = status == "submitted" && Scope(reviewed.TenantId, reviewed.SubjectId) &&
                        reviewed.Revision == next && reviewed.Version == businessVersion &&
                        (reviewed.Outcome is "approved" or "rejected") && reviewed.ActorMemberId != author &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(ReviewAuditResponse), (ulong)revision);
                    status = reviewed.Outcome;
                    break;
                case AuditorResponseDelivered delivered:
                    valid = delivery is null && (status is "submitted" or "approved") &&
                        Scope(delivered.TenantId, delivered.SubjectId) && delivered.Revision == next &&
                        delivered.Version == businessVersion &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(DeliverAuditResponse), (ulong)revision);
                    status = "delivered";
                    delivery = delivered;
                    break;
                case AuditorResponseDispositionRecorded recorded:
                    valid = delivery is not null && disposition is null && Scope(recorded.TenantId, recorded.SubjectId) &&
                        recorded.Revision == next && recorded.Version == delivery.Version &&
                        (recorded.Outcome is "accepted" or "rejected" or "changes_requested") &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(RecordAuditorResponseDisposition), (ulong)revision);
                    disposition = recorded;
                    break;
                default:
                    valid = false;
                    break;
            }
            if (revision > 0 && (uses.Active.Count != 0 || closedByGeneration is not null))
                valid = false;
            if (!valid)
                throw Invalid("A response history contains another scope, version, disclosure predecessor or root binding.");
            revision = next;
            latest = fact;
        }
        if (revision != owner.Revision || businessVersion != owner.BusinessVersion || status != owner.Status ||
            author != owner.AuthorMemberId || closedByGeneration != owner.ClosedByGeneration ||
            delivery?.Metadata.EventId != owner.Delivery?.Metadata.EventId ||
            disposition?.Metadata.EventId != owner.Disposition?.Metadata.EventId ||
            (latest is null) != (owner.Latest is null) || latest is not null &&
            ContentDigest.Sha256(latest, latest.GetType()) != ContentDigest.Sha256(owner.Latest!, owner.Latest!.GetType()))
            throw Invalid("The response history differs from current compact content authority.");
        owner.Uses.Confirm(uses, owner.Version, owner.CommittedStreamPosition);
        return Array.AsReadOnly(facts.ToArray());

        bool Scope(Uuid tenant, Uuid response) => tenant == owner.TenantId && response == owner.ResponseId;
    }

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
