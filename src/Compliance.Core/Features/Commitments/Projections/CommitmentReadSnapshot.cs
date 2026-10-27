using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Commitments.Projections;

public sealed class CommitmentReadSnapshot(CommitmentDraft owner, CommitmentRules rules, bool inScope = true)
{
    public CommitmentDraft Owner { get; } = owner;
    internal CommitmentRules Rules { get; } = rules;
    public Uuid TenantId => Owner.TenantId;
    public Uuid ProgramId => Owner.ProgramId;
    public Uuid Id => Owner.Id;
    public Uuid ServiceId => Owner.ServiceId;
    public Uuid? ProviderId => Owner.ProviderId;
    public string? Kind => Owner.Kind;
    public string? Identifier => Owner.Identifier;
    public long Revision => Owner.Revision;
    public ulong SourcePhysicalPosition => Owner.CommittedStreamPosition;
    public bool IsCreated => inScope && Owner.IsCreated;
    public bool IsRetired => inScope && Owner.IsRetired;
    public string? SourceReference => IsCreated ? Owner.SourceReference : null;
    public Uuid? AcceptedReviewDecisionId => IsCreated ? Owner.AcceptedReviewDecisionId : null;
    public Uuid? AcceptedReviewerMemberId => IsCreated ? Owner.AcceptedReviewerMemberId : null;
    public IReadOnlySet<Uuid> PendingAuthorMemberIds => IsCreated ? Rules.PendingAuthorMemberIds : new HashSet<Uuid>();
    public long EffectiveVersionCount => IsCreated ? Owner.EffectiveVersionCount : 0;
    public long? LatestEffectiveRevision => IsCreated ? Owner.LatestEffectiveRevision : null;
    public DateOnly? RetiredEffectiveOn => IsCreated ? Owner.RetiredEffectiveOn : null;
    public string? RetirementRationale => IsCreated ? Rules.RetirementRationale : null;
    public CommitmentRetiredContent? Retirement => IsCreated ? Rules.Retirement : null;
    public CommitmentDraftView? Draft => IsCreated ? Rules.DraftView() : null;
    public long? EffectiveVersionOn(DateOnly date) => IsCreated ? Rules.EffectiveVersionOn(date) : null;
    public IReadOnlyList<CommitmentVersionView> ReadVersions() => IsCreated ? Rules.ReadVersions() : [];
    public IReadOnlyList<CommitmentDecisionView> ReadDecisions() => IsCreated ? Rules.ReadDecisions() : [];
    public IReadOnlyList<CommitmentDraftRevisionView> ReadRevisions() => IsCreated ? Rules.ReadRevisions() : [];
    public CommitmentVersionView? FindVersion(long version) => IsCreated ? Rules.FindVersion(version) : null;
    public CommitmentDecisionView? FindDecision(Uuid decision) => IsCreated ? Rules.FindDecision(decision) : null;
    public CommitmentDraftRevisionView? FindRevision(long revision) => IsCreated ? Rules.FindRevision(revision) : null;
    public bool IsCurrentResponsibilityScope(ResponsibilityScope scope) => IsCreated && Owner.IsCurrentResponsibilityScope(scope);
    public CommitmentResponsibilityReadSnapshot GetResponsibilitySet(ResponsibilityScope scope) => new(TenantId, scope,
        IsCreated ? Rules.GetResponsibilitySet(scope).ReadAssignments() : []);
}
