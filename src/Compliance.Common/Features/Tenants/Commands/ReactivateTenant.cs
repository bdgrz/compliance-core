using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
namespace Bdgrz.Compliance.Features.Tenants.Commands;

[Discriminator("bdgrz.tenant.reactivate", 3)]
public sealed record ReactivateTenant(Uuid CommandId, Uuid TenantId, string Reason)
    : IRequest<UnitResult>, ICallable, IPlatformOperatorRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
