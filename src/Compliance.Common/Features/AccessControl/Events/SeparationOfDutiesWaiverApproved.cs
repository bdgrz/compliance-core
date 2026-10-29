using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.sod-waiver.approved", 1)]
public sealed record SeparationOfDutiesWaiverApproved(
    Uuid TenantId,
    Uuid WaiverId,
    Uuid ApproverMemberId,
    string ApproverDisplay,
    DateTimeOffset ApprovedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ApproverMemberId, ApproverDisplay);
}
