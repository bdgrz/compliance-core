using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Controls.Events;

/// <summary>
///     Designates, or clears with a null <c>PersonId</c>, the governed workforce person who owns
///     one exact pending control revision. The person need not sign in. The signed-in recording
///     actor is kept separate, and <c>CorrelatedMemberId</c> is snapshotted only to apply
///     owner-based separation of duties; it never grants access.
/// </summary>
[Discriminator("bdgrz.control.owner_person.designated", 2)]
public sealed record ControlOwnerPersonDesignated(Uuid TenantId, Uuid ProgramId,
    Uuid ControlId, Uuid VersionId, long Revision, Uuid DesignationId, Uuid? PersonId,
    Uuid? CorrelatedMemberId, string Rationale, Uuid ActorMemberId, ActorReference Actor,
    DateTimeOffset DesignatedAt) : DomainEvent, IControlFact
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
}
