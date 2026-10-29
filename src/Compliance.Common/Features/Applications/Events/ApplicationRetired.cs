using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Applications.Events;

/// <summary>An effective-dated retirement. History, instances, and snapshot references remain.</summary>
[Discriminator("bdgrz.application.retired", 1)]
public sealed record ApplicationRetired(Uuid TenantId, Uuid ApplicationId, long Revision,
    DateTimeOffset EffectiveAt, string Reason, Uuid? MergedIntoApplicationId,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    long? MergedIntoApplicationRevision = null) : DomainEvent, INativeOperationFact
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
