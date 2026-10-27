using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Controls.Contracts;
namespace Bdgrz.Compliance.Features.Controls.Models;

public sealed record ControlDraftCreatedContent(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid CreateRequestId, string Identifier, ControlDraftContent Content, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset ChangedAt) : IControlMutationContent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
