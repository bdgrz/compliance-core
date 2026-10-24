using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.NativeOperations;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Opens a successor version line from one exact approved version. The successor's authored
///     content is recorded by the <see cref="ControlDraftRevised" /> raised in the same commit.
/// </summary>
[Discriminator("bdgrz.control.successor.proposed", 2)]
public sealed record ControlSuccessorProposed(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid VersionId, Uuid PredecessorVersionId, long Revision, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent, IControlFact
{
    public ImmutableContentRef Content { get; init; } = default!;

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
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId,
        ActorDisplay);
}
