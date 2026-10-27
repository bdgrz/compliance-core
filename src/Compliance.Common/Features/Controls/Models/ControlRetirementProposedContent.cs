using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Proposes ending future use of one exact approved control version.</summary>
public sealed record ControlRetirementProposedContent(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid RetirementId, Uuid VersionId, long Revision, DateOnly EffectiveUntil, string Rationale,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt) : IControlMutationContent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId,
        ActorDisplay);
}
