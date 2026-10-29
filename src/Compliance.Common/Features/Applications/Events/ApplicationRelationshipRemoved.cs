using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Applications.Events;

[Discriminator("bdgrz.application.relationship.removed", 1)]
public sealed record ApplicationRelationshipRemoved(Uuid TenantId, Uuid ApplicationId,
    Uuid RelationshipId, long RelationshipRevision, Uuid TargetApplicationId,
    string RelationshipType, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ChangedAt) : DomainEvent, INativeOperationFact
{
    public OperationProvenance Operation { get; init; } = null!;
    public ImmutableContentRef? FirstResponse { get; init; }
    public ImmutableContentRef? OperationResult { get; init; }

    /// <summary>Reason recorded by the governed relationship removal contract, when present.</summary>
    public string? Reason { get; init; }

    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
