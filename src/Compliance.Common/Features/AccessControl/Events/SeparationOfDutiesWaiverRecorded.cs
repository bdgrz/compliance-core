using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.sod-waiver.recorded", 1)]
public sealed record SeparationOfDutiesWaiverRecorded(
    Uuid TenantId,
    Uuid WaiverId,
    SeparationOfDutiesWaiverScope Scope,
    Uuid BeneficiaryMemberId,
    Uuid RequesterMemberId,
    string RequesterDisplay,
    string Rationale,
    DateTimeOffset RequestedAt,
    DateTimeOffset ExpiresAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        RequesterMemberId, RequesterDisplay);
}
