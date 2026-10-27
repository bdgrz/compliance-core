using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.platform.operator.revoke", 1)]
public sealed record RevokePlatformOperator(Uuid UserId, string Reason, Uuid CommandId,
    ulong ExpectedRevision, Uuid AssignmentId) : IRequest, ICallable, IPlatformOperatorRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
