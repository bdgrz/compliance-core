using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>
///     The personal approval that makes the exact reviewed revision an immutable effective
///     version. HTTP-only. A successor requires the acknowledged impact preview digest.
/// </summary>
[Discriminator("bdgrz.policy.approve", 2)]
public sealed record ApprovePolicy(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long ExpectedRevision, Uuid AcceptedReviewDecisionId, DateOnly EffectiveFrom, bool Major,
    string Rationale, string? ImpactDigest = null, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<PolicyVersionView>, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
