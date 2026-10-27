using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Policies.Commands;

[Discriminator("bdgrz.policy.retirement.propose", 2)]
public sealed record ProposePolicyRetirement(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long ExpectedVersion, DateOnly EffectiveUntil, string Rationale)
    : IRequest<PolicyRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
