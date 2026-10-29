using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

[Discriminator("bdgrz.access-grant.revoke", 1)]
public sealed record RevokeAccessGrant(Uuid TenantId, Uuid GrantId)
    : IRequest, IClientRbacMutationRequest, ICallable;
