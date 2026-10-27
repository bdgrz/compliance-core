using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>
///     Records the periodic review of the current version without changing it, restarting its
///     review cadence. HTTP-only personal sign-off.
/// </summary>
[Discriminator("bdgrz.policy.periodic_review.confirm", 2)]
public sealed record ConfirmPolicyReview(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long ExpectedVersion, string Rationale)
    : IRequest<PolicyDecisionView>, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
