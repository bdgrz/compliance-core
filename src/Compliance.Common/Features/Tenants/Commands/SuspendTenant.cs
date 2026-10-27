using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Commands;

[Discriminator("bdgrz.tenant.suspend", 3)]
public sealed record SuspendTenant(Uuid CommandId, Uuid TenantId, string Reason, long ExpectedRevision)
    : IRequest<UnitResult>, ICallable, IPlatformOperatorRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
