using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Verified read composition at an exact native source cut; this state is never retained by the command owner.</summary>
public sealed class ControlReadSnapshot
{
    readonly bool _inScope;
    internal ControlRules Rules { get; }
    readonly IReadOnlyList<ControlRetired> _retirements;
    public ControlReadSnapshot(ControlDraft owner, ControlRules rules, ControlDraftView? draft,
        IReadOnlyList<ControlRetired> retirements, bool inScope = true)
    {
        (Owner, Rules, Draft, _retirements, _inScope) = (owner, rules, draft, retirements, inScope);
    }
    public ControlDraft Owner { get; }
    public ControlDraftView? Draft { get; }
    public Uuid TenantId => Owner.TenantId;
    public Uuid ProgramId => Owner.ProgramId;
    public Uuid Id => Owner.Id;
    public long Revision => Owner.Revision;
    public bool IsCreated => _inScope && Owner.IsCreated;
    public bool IsVisible => _inScope && Owner.IsVisible;
    public bool IsApproved => IsVisible && Owner.IsApproved;
    public bool IsRetired => _inScope && Owner.IsRetired;
    public bool HasOpenDraft => IsVisible && Owner.HasOpenDraft;
    public Uuid DraftVersionId => Owner.DraftVersionId;
    public Uuid? PendingTargetId => IsVisible ? Owner.PendingTargetId : null;
    public Uuid? PendingRetirementId => IsVisible ? Owner.PendingRetirementId : null;
    public DateOnly? PendingRetirementEffectiveUntil => IsVisible ? Owner.PendingRetirementEffectiveUntil : null;
    public Uuid? PendingAuthorMemberId => IsVisible ? Owner.PendingAuthorMemberId : null;
    public Uuid? AcceptedReviewDecisionId => IsVisible ? Owner.AcceptedReviewDecisionId : null;
    public Uuid? AcceptedReviewerMemberId => IsVisible ? Owner.AcceptedReviewerMemberId : null;
    public Uuid? LatestReviewDecisionId => IsVisible ? Owner.LatestReviewDecisionId : null;
    public DateTimeOffset? PendingDecisionChangedAt => IsVisible ? Owner.PendingDecisionChangedAt : null;
    public ControlDraftContent? CurrentContent => IsVisible ? Rules.CurrentContent : null;
    public ControlVersionView? ApprovedVersion => IsVisible ? Rules.ApprovedVersion : null;
    public ControlPersonOwner? CurrentPersonOwner => IsVisible ? Rules.CurrentPersonOwner : null;
    public ControlPersonOwnerDesignationView? CurrentPersonOwnerDesignation => IsVisible ? Rules.CurrentPersonOwnerDesignation : null;
    public IReadOnlyList<ControlVersionView> ReadVersions() => IsVisible ? Rules.ReadVersions() : [];
    public IReadOnlyList<ControlVersionView> ReadApprovedVersionHistory() => IsVisible ? Rules.ReadApprovedVersionHistory() : [];
    public IReadOnlyList<ControlRetired> ReadRetirementHistory() => IsVisible ? _retirements : [];
    public IReadOnlyList<ControlDecisionView> ReadDecisions() => IsVisible ? Rules.ReadDecisions() : [];
    public ControlVersionView? EffectiveVersion(DateOnly at) => IsVisible ? Rules.EffectiveVersion(at) : null;
    public bool IsCurrentResponsibilityScope(ResponsibilityScope scope) => IsVisible && Owner.IsCurrentResponsibilityScope(scope);
    public IReadOnlyList<ResponsibilityAssignmentView> RetainedResponsibilities(Uuid versionId, long revision) =>
        IsVisible ? Rules.RetainedResponsibilities(versionId, revision) : [];
    public ControlResponsibilityReadSnapshot GetResponsibilitySet(ResponsibilityScope scope) => new(TenantId, scope,
        IsVisible ? Rules.GetResponsibilitySet(scope).ReadAssignments() : []);
    public IReadOnlyList<Uuid> CurrentOwnerMemberIds(DateTimeOffset at) => IsVisible ? Rules.CurrentOwnerMemberIds(at) : [];
}
