using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Rebuilds the full campaign prefix without retaining child/root history in command authority.</summary>
public static class AccessReviewCampaignHistory
{
    public static async Task<AccessReviewCampaignHistorySnapshot> ReadAsync(IDomainEventReader events,
        AccessReviewCampaign owner, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var logical = new HashSet<Uuid>();
        var digest = AccessReviewReplayDigest.Empty;
        AccessReviewCampaignLaunched? launched = null;
        AccessReviewCampaignCompletionAccepted? pending = null;
        AccessReviewCampaignCompleted? closed = null;
        long revision = 0;
        foreach (var fact in facts)
        {
            if (fact is not INativeOperationFact native || native.Operation.Owner != owner.Stream ||
                native.Operation.LogicalId == Uuid.Empty || !logical.Add(native.Operation.LogicalId))
                throw Invalid("The campaign history contains another owner or repeated logical operation.");
            digest = AccessReviewReplayDigest.Append(digest, fact);
            if (native.Operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                (owner.Acceptance.SelectedFact is not { } selected || selected.GetType() != fact.GetType() ||
                    !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                throw Invalid("The selected campaign acceptance differs from its actual payload and complete metadata.");
            var operation = native.Operation;
            var open = launched is not null && pending is null && closed is null;
            bool valid;
            switch (fact)
            {
                case AccessReviewCampaignLaunched launch:
                    valid = launched is null && revision == 0 && launch.TenantId == owner.TenantId &&
                        launch.CampaignId == owner.Id && operation.LogicalId == owner.Id && launch.FirstResponse is not null &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(LaunchAccessReviewCampaign), null);
                    launched = launch;
                    break;
                case AccessReviewDecisionBatchAdmitted batch:
                    valid = open && batch.Owner == owner.Stream && batch.Revision == revision + 1 &&
                        batch.Generation == AccessReviewEffects.Generation && batch.FirstResponse is not null &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(RecordBulkAccessDecision), (ulong)revision);
                    break;
                case AccessReviewCampaignCompletionAccepted accepted:
                    valid = open && accepted.Owner == owner.Stream && accepted.Revision == revision + 1 &&
                        accepted.Generation == AccessReviewEffects.Generation && accepted.FirstResponse is not null &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(CompleteAccessReviewCampaign), (ulong)revision);
                    pending = accepted;
                    break;
                case AccessReviewCampaignCompleted completed:
                    valid = pending is not null && completed.TenantId == owner.TenantId && completed.CampaignId == owner.Id &&
                        completed.Revision == revision + 1 && completed.CampaignGeneration == AccessReviewEffects.Generation &&
                        completed.Intent == pending.Intent && completed.OperationResult is not null &&
                        Child(operation, pending, nameof(CompleteAccessReviewCampaignClosure));
                    closed = completed;
                    pending = null;
                    break;
                case AccessReviewCampaignCompletionCanceled canceled:
                    valid = pending is not null && canceled.Owner == owner.Stream && canceled.Revision == revision + 1 &&
                        canceled.Generation == AccessReviewEffects.Generation && canceled.Intent == pending.Intent &&
                        canceled.SourceProofs is not null && Child(operation, pending, nameof(AbortAccessReviewCampaignCompletion));
                    pending = null;
                    break;
                default:
                    valid = false;
                    break;
            }
            if (!valid)
                throw Invalid("The campaign prefix has another scope, admission, seal, root binding or terminal predecessor.");
            revision++;
        }
        if (revision != owner.Revision || launched?.Metadata.EventId != owner.Launched?.Metadata.EventId ||
            pending?.Metadata.EventId != owner.Pending?.Metadata.EventId || closed?.Metadata.EventId != owner.Closed?.Metadata.EventId ||
            (facts.Count == 0) != (owner.Latest is null) || facts.Count != 0 &&
                ContentDigest.Sha256(facts[^1], facts[^1].GetType()) != ContentDigest.Sha256(owner.Latest!, owner.Latest!.GetType()))
            throw Invalid("The verified campaign prefix differs from compact current authority.");
        owner.ReplayCut.Confirm(owner, digest);
        ct.ThrowIfCancellationRequested();
        return new(owner, facts);
    }

    static bool Child(OperationProvenance operation, AccessReviewCampaignCompletionAccepted accepted, string kind) =>
        operation.RootOperationId == accepted.Operation.RootOperationId && operation.CommandKind == kind &&
        operation.CausationEventId == accepted.Metadata.EventId && operation.FirstOwner == accepted.Owner &&
        operation.AffectedOwner == accepted.Owner && operation.ExpectedRevisionOwner == accepted.Owner &&
        operation.Actor == accepted.Operation.Actor && operation.Episode == accepted.Operation.Episode &&
        operation.OriginalExpectedRevision == accepted.Operation.OriginalExpectedRevision &&
        operation.AuthoredIntentSha256 == accepted.Operation.AuthoredIntentSha256;
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
