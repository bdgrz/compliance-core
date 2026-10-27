using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.NativeOperations;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls.Events;

/// <summary>
///     Withdraws a pending successor draft or retirement proposal at its exact revision. The
///     current approved version stays effective; the <see cref="ControlDraftRevised" /> raised in
///     the same commit restores its content on a new revision so stale decisions cannot apply.
/// </summary>
/// <param name="Kind">successor or retirement.</param>
/// <param name="TargetId">The withdrawn successor version ID or retirement proposal ID.</param>
/// <param name="CurrentVersionId">The approved version that remains current.</param>
[Discriminator("bdgrz.control.proposal.withdrawn", 2)]
public sealed record ControlProposalWithdrawn(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid DecisionId, string Kind, Uuid TargetId, Uuid CurrentVersionId, long Revision,
    string Rationale, Uuid ActorMemberId, ActorReference Actor, DateTimeOffset WithdrawnAt)
    : DomainEvent, IControlFact
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
}
