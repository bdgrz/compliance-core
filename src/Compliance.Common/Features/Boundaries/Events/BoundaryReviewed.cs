using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.reviewed", 2)]
public sealed record BoundaryReviewed(Uuid TenantId, Uuid BoundaryId, Uuid DraftVersionId, long Revision, Uuid DecisionId, string Outcome, Uuid ActorMemberId, string ActorDisplay, DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId = null) : DomainEvent, IBoundaryFact
{
    public OperationProvenance Operation { get; init; } = default!;
    public ImmutableContentRef? FirstResponse { get; init; }
    public ImmutableContentRef? OperationResult { get; init; }
    public BoundaryAuthorityHeader Header { get; init; } = default!;
    public ImmutableContentRef Change { get; init; } = default!;
    public ImmutableContentRef Authority { get; init; } = default!;
    public long NativeSequence { get; init; }
    public int ChangeCount { get; init; }
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }
    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
