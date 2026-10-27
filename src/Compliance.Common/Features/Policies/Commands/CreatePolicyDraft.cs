using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Policies.Commands;

[Discriminator("bdgrz.policy.draft.create", 2)]
public sealed record CreatePolicyDraft(Uuid PolicyId, Uuid TenantId, Uuid ProgramId, string Identifier,
    PolicyContent Content)
    : IRequest<PolicyRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => PolicyId;
}
