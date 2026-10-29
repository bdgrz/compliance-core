using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
namespace Bdgrz.Compliance.Features.Boundaries.Models;

public sealed record BoundaryDraftDiscardedContent(Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, long Revision, Uuid ActorMemberId,
    string ActorDisplay, string Rationale, DateTimeOffset DiscardedAt) : IBoundaryMutationContent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
