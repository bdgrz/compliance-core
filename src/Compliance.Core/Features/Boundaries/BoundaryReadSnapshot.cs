using Bdgrz.Compliance.Features.Boundaries.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class BoundaryReadSnapshot(SystemBoundary owner, BoundaryRules rules, bool inScope = true)
{
    public SystemBoundary Owner { get; } = owner;
    internal BoundaryRules Rules { get; } = rules;
    public Uuid TenantId => Owner.TenantId;
    public Uuid ProgramId => Owner.ProgramId;
    public Uuid Id => Owner.Id;
    public long Revision => Owner.Revision;
    public ulong SourcePhysicalPosition => Owner.CommittedStreamPosition;
    public bool IsCreated => inScope && Owner.IsCreated;
    public bool IsVisible => IsCreated && Owner.IsVisible;
    public Uuid DraftVersionId => IsCreated ? Owner.DraftVersionId : Uuid.Empty;
    public long DraftRevision => IsCreated ? Owner.DraftRevision : 0;
    public Uuid DraftAuthorMemberId => IsCreated ? Owner.DraftAuthorMemberId : Uuid.Empty;
    public DateTimeOffset? DraftChangedAt => IsCreated ? Owner.DraftChangedAt : null;
    public string? LatestReviewOutcome => IsCreated ? Owner.LatestReviewOutcome : null;
    public Uuid LatestApprovedVersionId => IsCreated ? Owner.LatestApprovedVersionId : Uuid.Empty;
    public Uuid FirstApprovedVersionId => IsCreated ? Owner.FirstApprovedVersionId : Uuid.Empty;
    public BoundaryVersionView? Draft => IsCreated ? Rules.Draft : null;
    public BoundaryVersionView? LatestApprovedVersion => IsCreated ? Rules.LatestApprovedVersion : null;
    public BoundaryDecisionView? LatestDecision => IsCreated ? Rules.LatestDecision : null;
    public BoundaryView? ToView() => IsVisible ? Rules.ToView() : null;
    public bool IsVersionApproved(Uuid version) => IsCreated && Rules.IsVersionApproved(version);
    public bool IsVersionApproved(Uuid version, long revision, Uuid decision) => IsCreated && Rules.IsVersionApproved(version, revision, decision);
    public IReadOnlyList<BoundaryVersionView> ReadVersions() => IsCreated ? Rules.ReadVersions() : [];
    public IReadOnlyList<BoundaryDecisionView> ReadDecisions() => IsCreated ? Rules.ReadDecisions() : [];
    public BoundaryVersionView? FindVersion(Uuid version) => IsCreated ? Rules.FindVersion(version) : null;
    public BoundaryDecisionView? FindDecision(Uuid decision) => IsCreated ? Rules.FindDecision(decision) : null;
    public BoundaryVersionView? EffectiveVersion(DateOnly at) => IsCreated ? Rules.EffectiveVersion(at) : null;
    public bool IsCurrentResponsibilityScope(ResponsibilityScope scope) => IsCreated && Owner.IsCurrentResponsibilityScope(scope);
    public BoundaryResponsibilityReadSnapshot GetResponsibilitySet(ResponsibilityScope scope) => new(TenantId, scope,
        IsCreated ? Rules.GetResponsibilitySet(scope).ReadAssignments() : []);
}
