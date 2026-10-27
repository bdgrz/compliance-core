using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Controls.Models;

public sealed record ControlDraftRevisionView(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    string Identifier, long Revision, ControlDraftContent Content,
    Uuid ChangedByMemberId, string ChangedByDisplay, DateTimeOffset ChangedAt)
{
    readonly ActorReference? _actor;

    /// <summary>The snapshotted actor; pre-snapshot rows fall back to the member columns.</summary>
    [JsonPropertyName("actor")]
    public ActorReference Actor
    {
        get => _actor ?? ActorReference.ForMember(ChangedByMemberId, ChangedByDisplay);
        init => _actor = value;
    }
}
