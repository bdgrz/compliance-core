using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Contracts;
namespace Bdgrz.Compliance.Features.Tenants.Commands;

[Discriminator("bdgrz.platform.operator.revoke", 1)]
public sealed record RevokePlatformOperator(Uuid UserId, string Reason, Uuid CommandId,
    ulong ExpectedRevision, Uuid AssignmentId) : IRequest, ICallable, IPlatformOperatorRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
