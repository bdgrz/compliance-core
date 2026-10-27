using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Commitments.Models;

public sealed record CommitmentDraftCreatedContent(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    Uuid CreateRequestId, Uuid ServiceId, string Kind, string Identifier,
    string Statement, string Context, string SourceReference,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt) : ICommitmentMutationContent
{
    /// <summary>The provider that performs a canonical CSOC; absent on legacy or non-CSOC drafts.</summary>
    public Uuid? ProviderId { get; init; }

    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
