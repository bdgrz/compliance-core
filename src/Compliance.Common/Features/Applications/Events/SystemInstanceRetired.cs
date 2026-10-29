using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Applications.Events;

/// <summary>An effective-dated instance retirement. Scope decisions and history remain.</summary>
[Discriminator("bdgrz.system_instance.retired", 1)]
public sealed record SystemInstanceRetired(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long Revision, DateTimeOffset EffectiveAt, string Reason,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
