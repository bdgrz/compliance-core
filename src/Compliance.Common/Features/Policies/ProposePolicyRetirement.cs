using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

[Discriminator("bdgrz.policy.retirement.propose", 2)]
public sealed record ProposePolicyRetirement(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long ExpectedVersion, DateOnly EffectiveUntil, string Rationale)
    : IRequest<PolicyRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
