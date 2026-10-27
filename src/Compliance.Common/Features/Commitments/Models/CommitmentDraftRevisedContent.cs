using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.AccessControl.Models;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments.Models;

public sealed record CommitmentDraftRevisedContent(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long Revision, string Statement, string Context, string SourceReference,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt) : ICommitmentMutationContent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
