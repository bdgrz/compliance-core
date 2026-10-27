using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Complete transient campaign history and exact membership proof at one verified native/physical cut.</summary>
public sealed class AccessReviewCampaignHistorySnapshot
{
    readonly AccessReviewPreparedCut _cut;
    internal AccessReviewCampaignHistorySnapshot(AccessReviewCampaign owner, IReadOnlyList<DomainEvent> facts)
    {
        _cut = AccessReviewPreparedCut.Capture(owner);
        Facts = Array.AsReadOnly(facts.ToArray());
        AdmittedBatches = Array.AsReadOnly(facts.OfType<AccessReviewDecisionBatchAdmitted>().ToArray());
    }
    public IReadOnlyList<DomainEvent> Facts { get; }
    public IReadOnlyList<AccessReviewDecisionBatchAdmitted> AdmittedBatches { get; }
    public AccessReviewDecisionBatchAdmitted? AdmittedFor(Uuid id) => AdmittedBatches.SingleOrDefault(
        fact => fact.Operation.RootOperationId == id);
    public AccessReviewCampaignCompletionAccepted? AcceptedFor(Uuid id) => Facts.OfType<AccessReviewCampaignCompletionAccepted>()
        .SingleOrDefault(fact => fact.Operation.RootOperationId == id);
    public AccessReviewCampaignCompletionCanceled? CanceledFor(Uuid id) => Facts.OfType<AccessReviewCampaignCompletionCanceled>()
        .SingleOrDefault(fact => fact.Operation.RootOperationId == id);
    public bool Matches(AccessReviewCampaign owner) => _cut.Matches(owner);
}
