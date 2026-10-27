using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.AccessControl.Models;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls.Models;

public sealed record ControlDraftDiscardedContent(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long Revision, Uuid ActorMemberId, string ActorDisplay, string Rationale,
    DateTimeOffset DiscardedAt) : IControlMutationContent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
