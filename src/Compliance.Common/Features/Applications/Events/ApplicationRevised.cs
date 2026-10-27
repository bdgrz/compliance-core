using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Applications.Events;

[Discriminator("bdgrz.application.revised", 1)]
public sealed record ApplicationRevised(Uuid TenantId, Uuid ApplicationId, long Revision,
    string Name, string Purpose, string? OwnerReference,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    string? Classification = null, Uuid? SystemOwnerPersonId = null,
    Uuid? AccessOwnerPersonId = null, bool IsRestricted = false,
    string? ApplicationKind = null, string? Criticality = null) : DomainEvent, INativeOperationFact
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
