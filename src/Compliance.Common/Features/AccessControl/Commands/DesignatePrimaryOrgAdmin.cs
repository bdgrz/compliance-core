using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

[Discriminator("bdgrz.rbac.designate-primary-org-admin", 1)]
public sealed record DesignatePrimaryOrgAdmin(Uuid CommandId, Uuid TenantId, Uuid UserId, Uuid MembershipEpisodeId) : IRequest, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
