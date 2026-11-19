using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Commands;

/// <summary>Designates one existing operator as a protected platform primary.</summary>
[Discriminator("bdgrz.platform.primary.designate", 1)]
public sealed record DesignatePlatformPrimary(Uuid UserId, Uuid CommandId, ulong ExpectedRevision)
    : IRequest, ICallable, IIdentifiedRequest, IPlatformOperatorRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
