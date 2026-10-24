using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Controls;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

[Discriminator("bdgrz.responsibility.assigned", 1)]
public sealed record ResponsibilityAssigned(Uuid TenantId, Uuid AssignmentId,
    Uuid MemberId, ResponsibilityType Type, ResponsibilityScope Scope,
    DateTimeOffset AssignedAt, Uuid AssignedByMemberId,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil,
    IReadOnlyList<Uuid> SeparationOfDutiesWaiverIds, string AssignedByDisplay) : DomainEvent, IControlFact, IControlMutationContent, ICommitmentFact, ICommitmentMutationContent, IBoundaryFact, IBoundaryMutationContent
{
    /// <summary>The exact native acceptance and immutable original result.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OperationProvenance Operation { get; init; } = default!;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableContentRef? FirstResponse { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableContentRef? OperationResult { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ControlAuthorityHeader Header { get; init; } = default!;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableContentRef Change { get; init; } = default!;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableContentRef Authority { get; init; } = default!;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long NativeSequence { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ChangeCount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CommitmentAuthorityHeader? CommitmentHeader { get; init; }
    CommitmentAuthorityHeader ICommitmentFact.Header => CommitmentHeader!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BoundaryAuthorityHeader? BoundaryHeader { get; init; }
    BoundaryAuthorityHeader IBoundaryFact.Header => BoundaryHeader!;

    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        AssignedByMemberId, AssignedByDisplay);
}
