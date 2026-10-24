using Bdgrz.Compliance.Features.NativeOperations;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.reviewed", 1)]
public sealed record ControlReviewed(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid VersionId, long Revision, Uuid DecisionId, string Outcome, Uuid ActorMemberId,
    string ActorDisplay, string Rationale, DateTimeOffset DecidedAt,
    Uuid? SupersedesDecisionId = null, Uuid? SeparationOfDutiesWaiverId = null) : DomainEvent
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OperationProvenance? Operation { get; init; }

    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
