using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant.reactivate", 3)]
public sealed record ReactivateTenant(Uuid CommandId, Uuid TenantId, string Reason)
    : IRequest<UnitResult>, ICallable, IPlatformOperatorRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
