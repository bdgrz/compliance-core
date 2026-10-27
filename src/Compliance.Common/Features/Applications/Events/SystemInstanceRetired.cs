using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

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
