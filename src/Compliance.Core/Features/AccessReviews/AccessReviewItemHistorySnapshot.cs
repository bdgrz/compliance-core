using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Transient complete native/item business history; all publication and fence maps stay outside authority.</summary>
public sealed class AccessReviewItemHistorySnapshot
{
    readonly Dictionary<Uuid, AccessDecisionsRecorded> _provisional;
    readonly Dictionary<Uuid, AccessReviewItemDecisionCanceled> _fences;
    readonly HashSet<Uuid> _published;
    readonly AccessReviewPreparedCut _cut;
    internal AccessReviewItemHistorySnapshot(AccessReviewItem owner, IReadOnlyList<DomainEvent> facts,
        IReadOnlyList<DomainEvent> business, Dictionary<Uuid, AccessDecisionsRecorded> provisional,
        Dictionary<Uuid, AccessReviewItemDecisionCanceled> fences, HashSet<Uuid> published)
    {
        _cut = AccessReviewPreparedCut.Capture(owner);
        Facts = Array.AsReadOnly(facts.ToArray());
        BusinessFacts = Array.AsReadOnly(business.ToArray());
        _provisional = new(provisional);
        _fences = new(fences);
        _published = [.. published];
    }
    public IReadOnlyList<DomainEvent> Facts { get; }
    public IReadOnlyList<DomainEvent> BusinessFacts { get; }
    public AccessDecisionsRecorded? ProvisionalFor(Uuid id) => _provisional.GetValueOrDefault(id);
    public AccessReviewItemDecisionCanceled? FenceFor(Uuid id) => _fences.GetValueOrDefault(id);
    public bool IsPublished(Uuid id) => _published.Contains(id);
    public bool IsFenced(Uuid id) => _fences.ContainsKey(id);
    public bool IsVisible(AccessDecisionsRecorded decision) => decision.DecisionBatchId is not { } batch || IsPublished(batch);
    public bool Matches(AccessReviewItem owner) => _cut.Matches(owner);
}
