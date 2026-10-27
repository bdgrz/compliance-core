using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Responsibilities.Events;

[Discriminator("bdgrz.responsibility.revoked", 1)]
public sealed record ResponsibilityRevoked(Uuid TenantId, Uuid AssignmentId,
    ResponsibilityScope Scope, Uuid RevokedByMemberId, DateTimeOffset RevokedAt, string Reason,
    string RevokedByDisplay) : DomainEvent, IControlFact, IControlMutationContent, ICommitmentFact, ICommitmentMutationContent, IBoundaryFact, IBoundaryMutationContent
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
        RevokedByMemberId, RevokedByDisplay);
}
