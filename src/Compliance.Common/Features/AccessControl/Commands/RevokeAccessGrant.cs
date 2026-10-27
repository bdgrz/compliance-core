using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

[Discriminator("bdgrz.access-grant.revoke", 1)]
public sealed record RevokeAccessGrant(Uuid TenantId, Uuid GrantId)
    : IRequest, IClientRbacMutationRequest, ICallable;
