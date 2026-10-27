using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Commands;

public sealed record SeedPlatformOperatorRoster(Uuid RosterId, Uuid[] UserIds) : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => RosterId;
}
