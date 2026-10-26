using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Events;

[Discriminator("bdgrz.application.relationship.approved", 1)]
public sealed record ApplicationRelationshipApproved(Uuid TenantId, Uuid RelationshipId,
    Uuid SourceApplicationId, Uuid TargetApplicationId, long Revision,
    long SourceApplicationRevision, long TargetApplicationRevision, string ImpactDigest,
    Uuid ApproverMemberId, string ApproverDisplay, DateTimeOffset ApprovedAt) : DomainEvent,
    INativeOperationFact
{
    public OperationProvenance Operation { get; init; } = null!;
    public ImmutableContentRef? FirstResponse { get; init; }
    public ImmutableContentRef? OperationResult { get; init; }

    [JsonPropertyName("approver")]
    public ActorReference? StoredApprover { get; init; }

    [JsonIgnore]
    public ActorReference Approver => StoredApprover ??
        ActorReference.ForMember(ApproverMemberId, ApproverDisplay);
}
