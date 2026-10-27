using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Records the SystemInstanceRevised domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.applications.system-instance-revised", 1)]
public sealed record SystemInstanceRevised(
    Uuid TenantId,
    Uuid ApplicationId,
    Uuid SystemInstanceId,
    long Revision,
    string Name,
    string Kind,
    string? AccessBoundaryReference,
    string? SourceIdentifier,
    string Rationale,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset ChangedAt,
    OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
