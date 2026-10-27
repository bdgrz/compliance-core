using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Opens a successor version line from one exact approved version. The successor's authored
///     content is recorded by the <see cref="ControlDraftRevised" /> raised in the same commit.
/// </summary>
public sealed record ControlSuccessorProposedContent(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid VersionId, Uuid PredecessorVersionId, long Revision, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset ChangedAt) : IControlMutationContent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId,
        ActorDisplay);
}
