using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AccessReviews.Projections;

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
