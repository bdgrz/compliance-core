using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.AccessControl.Models;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

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
