using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Applications.Events;

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
