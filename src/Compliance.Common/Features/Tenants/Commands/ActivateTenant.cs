using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Commands;

[Discriminator("bdgrz.tenant.activate", 1)]
public sealed record ActivateTenant(Uuid CommandId, Uuid TenantId, Uuid FirstAdministratorUserId,
    string? FirstAdministratorEmail = null) : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
