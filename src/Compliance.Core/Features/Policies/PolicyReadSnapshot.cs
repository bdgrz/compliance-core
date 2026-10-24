using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed class PolicyReadSnapshot(Policy owner, PolicyRules rules, bool inScope = true)
{
    public Policy Owner { get; } = owner;
    internal PolicyRules Rules { get; } = rules;
    public Uuid TenantId => Owner.TenantId;
    public Uuid ProgramId => Owner.ProgramId;
    public Uuid Id => Owner.Id;
    public string? Identifier => Owner.Identifier;
    public long Revision => Owner.Revision;
    public ulong SourcePhysicalPosition => Owner.CommittedStreamPosition;
    public bool IsCreated => inScope && Owner.IsCreated;
    public bool IsVisible => inScope && Owner.IsVisible;
    public bool IsRetired => inScope && Owner.IsRetired;
    public PolicyContent? DraftContent => IsVisible ? Rules.DraftContent : null;
    public long? DraftPredecessorVersion => IsVisible ? Rules.DraftPredecessorVersion : null;
    public IReadOnlySet<Uuid> PendingAuthorMemberIds => IsVisible ? Rules.PendingAuthorMemberIds : new HashSet<Uuid>();
    public Uuid? AcceptedReviewerMemberId => IsVisible ? Rules.AcceptedReviewerMemberId : null;
    public PolicyVersionView? CurrentVersion => IsVisible ? Rules.CurrentVersion : null;
    public DateOnly? NextReviewDueOn => IsVisible ? Rules.NextReviewDueOn : null;
    public string Status => Rules.Status;
    public string? PendingStatus => IsVisible ? Rules.PendingStatus : null;
    public IReadOnlyList<PolicyVersionView> ReadVersions() => IsVisible ? Rules.ReadVersions() : [];
    public IReadOnlyList<PolicyDecisionView> ReadDecisions() => IsVisible ? Rules.ReadDecisions() : [];
    public PolicyVersionView? FindVersion(long version) => IsVisible ? Rules.FindVersion(version) : null;
    public PolicyDecisionView? FindDecision(Uuid decision) => IsVisible ? Rules.FindDecision(decision) : null;
    public PolicyVersionView? EffectiveVersion(DateOnly date) => IsVisible ? Rules.EffectiveVersion(date) : null;
    public PolicyView ToView(DateOnly today) => IsVisible ? Rules.ToView(today)
        : throw new InvalidOperationException("A missing Policy has no view.");
}
