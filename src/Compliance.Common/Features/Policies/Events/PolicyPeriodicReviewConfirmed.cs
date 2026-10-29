using Cntryl.Portia;
using Bdgrz.Compliance.Features.Policies.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Policies.Events;

/// <summary>A periodic review that confirmed the current version without changing it.</summary>
[Discriminator("bdgrz.policy.periodic_review.confirmed", 2)]
public sealed record PolicyPeriodicReviewConfirmed(Uuid TenantId, Uuid ProgramId,
    Uuid PolicyId, long Version, Uuid DecisionId, DateOnly ReviewedOn, string Rationale,
    ActorReference Actor, Uuid ActorMemberId, DateTimeOffset DecidedAt) : DomainEvent, IPolicyFact
{
    public OperationProvenance Operation { get; init; } = default!;
    public ImmutableContentRef? FirstResponse { get; init; }
    public ImmutableContentRef? OperationResult { get; init; }
    public PolicyAuthorityHeader Header { get; init; } = default!;
    public ImmutableContentRef Change { get; init; } = default!;
    public ImmutableContentRef Authority { get; init; } = default!;
    public long NativeSequence { get; init; }
}
