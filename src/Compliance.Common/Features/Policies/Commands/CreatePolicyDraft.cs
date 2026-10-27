using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

[Discriminator("bdgrz.policy.draft.create", 2)]
public sealed record CreatePolicyDraft(Uuid PolicyId, Uuid TenantId, Uuid ProgramId, string Identifier,
    PolicyContent Content)
    : IRequest<PolicyRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => PolicyId;
}
