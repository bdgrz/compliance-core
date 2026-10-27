using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed record ControlDraftRevisedContent(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long Revision, ControlDraftContent Content, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset ChangedAt) : IControlMutationContent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
