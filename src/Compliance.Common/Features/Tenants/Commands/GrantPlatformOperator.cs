using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Contracts;
namespace Bdgrz.Compliance.Features.Tenants.Commands;

[Discriminator("bdgrz.platform.operator.grant", 1)]
public sealed record GrantPlatformOperator(Uuid UserId, string Reason, Uuid CommandId,
    ulong ExpectedRevision) : IRequest, ICallable, IPlatformOperatorRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
