using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant.offboard", 2)]
public sealed record OffboardTenant(Uuid CommandId, Uuid TenantId, string Reason, long ExpectedRevision)
    : IRequest<UnitResult>, ICallable, IPlatformOperatorRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
