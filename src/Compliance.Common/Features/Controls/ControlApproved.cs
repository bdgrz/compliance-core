using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.NativeOperations;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Activates an immutable control version from one exact reviewed draft revision.</summary>
[Discriminator("bdgrz.control.approved", 2)]
public sealed record ControlApproved(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid VersionId, long Revision, Uuid ApprovalDecisionId, Uuid AcceptedReviewDecisionId,
    ImmutableContentRef Content, Uuid OwnerAssignmentId, Uuid OwnerMemberId,
    Uuid ActorMemberId, string ActorDisplay, string Rationale, DateOnly EffectiveFrom,
    DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId = null,
    Uuid? PredecessorVersionId = null, string? ImpactDigest = null,
    Uuid? OwnerPersonId = null) : DomainEvent, IControlFact
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

    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(
        ActorMemberId, ActorDisplay);
}
