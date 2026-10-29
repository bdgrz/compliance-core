using Cntryl.Portia;
using Bdgrz.Compliance.Features.Policies.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Policies.Events;

/// <summary>
///     Activates the exact reviewed draft revision as an immutable version identified by
///     <c>ContentSha256</c>. A major version requires every audience to re-acknowledge.
/// </summary>
[Discriminator("bdgrz.policy.approved", 2)]
public sealed record PolicyApproved(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, long Version, bool Major, Uuid DecisionId, Uuid AcceptedReviewDecisionId,
    string ContentSha256, DateOnly EffectiveFrom, long? PredecessorVersion,
    string? ImpactDigest, string Rationale, ActorReference Actor, Uuid ActorMemberId,
    DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId) : DomainEvent, IPolicyFact
{
    public OperationProvenance Operation { get; init; } = default!;
    public ImmutableContentRef? FirstResponse { get; init; }
    public ImmutableContentRef? OperationResult { get; init; }
    public PolicyAuthorityHeader Header { get; init; } = default!;
    public ImmutableContentRef Change { get; init; } = default!;
    public ImmutableContentRef Authority { get; init; } = default!;
    public long NativeSequence { get; init; }
}
