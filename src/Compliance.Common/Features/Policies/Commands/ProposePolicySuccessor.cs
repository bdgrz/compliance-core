using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Policies.Commands;

/// <summary>Opens a successor draft from the exact current approved version.</summary>
[Discriminator("bdgrz.policy.successor.propose", 2)]
public sealed record ProposePolicySuccessor(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long ExpectedVersion, PolicyContent Content)
    : IRequest<PolicyRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
