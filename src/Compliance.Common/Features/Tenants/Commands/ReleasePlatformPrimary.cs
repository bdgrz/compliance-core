using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Commands;

/// <summary>Releases a primary after another confirmed primary exists.</summary>
[Discriminator("bdgrz.platform.primary.release", 1)]
public sealed record ReleasePlatformPrimary(Uuid UserId, ulong Generation, Uuid CommandId,
    ulong ExpectedRevision, string Reason) : IRequest, ICallable, IIdentifiedRequest, IPlatformOperatorRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
