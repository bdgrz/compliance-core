using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

[Discriminator("bdgrz.rbac.release-primary-org-admin", 1)]
public sealed record ReleasePrimaryOrgAdmin(Uuid CommandId, Uuid TenantId, Uuid UserId, Uuid DesignationId, string Reason) : IRequest, IClientRbacMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
