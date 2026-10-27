using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record BoundaryReviewedContent(Uuid TenantId, Uuid BoundaryId, Uuid DraftVersionId,
    long Revision, Uuid DecisionId, string Outcome, Uuid ActorMemberId,
    string ActorDisplay, string Rationale, DateTimeOffset DecidedAt,
    Uuid? SeparationOfDutiesWaiverId = null) : IBoundaryMutationContent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
