using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Applications.Events;

[Discriminator("bdgrz.system_instance.declared", 1)]
public sealed record SystemInstanceDeclared(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long ApplicationRevision, string Name, string Kind,
    string? AccessBoundaryReference, string? SourceIdentifier,
    Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ChangedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
