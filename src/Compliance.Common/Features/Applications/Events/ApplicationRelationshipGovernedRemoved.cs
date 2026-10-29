using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Applications.Events;

/// <summary>Version 2 removal event retains the original v1 event reader contract.</summary>
[Discriminator("bdgrz.application.relationship.removed", 2)]
public sealed record ApplicationRelationshipGovernedRemoved(Uuid TenantId,
    Uuid RelationshipId, Uuid SourceApplicationId, Uuid TargetApplicationId,
    string RelationshipType, long Revision, string Reason, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
