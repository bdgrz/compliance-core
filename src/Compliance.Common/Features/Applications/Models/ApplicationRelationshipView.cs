using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Models;

public sealed record ApplicationRelationshipView(Uuid RelationshipId,
    Uuid TargetApplicationId, string RelationshipType, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset ChangedAt)
{
    public Uuid TenantId { get; init; }
    public Uuid SourceApplicationId { get; init; }
    public long Revision { get; init; }
    public string Status { get; init; } = "active";
    public ActorReference ChangedBy { get; init; } =
        ActorReference.ForMember(ActorMemberId, ActorDisplay);
    public long SourceApplicationRevision { get; init; }
    public long TargetApplicationRevision { get; init; }
    public string? RemovalReason { get; init; }
    public ApplicationRelationshipApproval? Approval { get; init; }
}

public sealed record ApplicationRelationshipApproval(Uuid ApproverMemberId,
    ActorReference Approver, long SourceApplicationRevision,
    long TargetApplicationRevision, string ImpactDigest, DateTimeOffset ApprovedAt);
