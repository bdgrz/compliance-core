using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

[Discriminator("bdgrz.commitment.draft.created", 2)]
public sealed record CommitmentDraftCreated(Uuid TenantId, Uuid ProgramId, Uuid DraftId, Uuid CreateRequestId, Uuid ServiceId,
    string Kind, string Identifier, ImmutableContentRef Content, string SourceReference,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent, ICommitmentFact
{
    public OperationProvenance Operation { get; init; } = default!;
    public ImmutableContentRef? FirstResponse { get; init; }
    public ImmutableContentRef? OperationResult { get; init; }
    public CommitmentAuthorityHeader Header { get; init; } = default!;
    public ImmutableContentRef Change { get; init; } = default!;
    public ImmutableContentRef Authority { get; init; } = default!;
    public long NativeSequence { get; init; }
    public int ChangeCount { get; init; }
    public Uuid? ProviderId { get; init; }
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }
    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
