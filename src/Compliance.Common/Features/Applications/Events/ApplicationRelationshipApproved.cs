using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
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
