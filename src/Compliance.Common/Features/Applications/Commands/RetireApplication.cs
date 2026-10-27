using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Commands;

/// <summary>Retires an application; <paramref name="CommandId" /> is the logical identity of the operation.</summary>
[Discriminator("bdgrz.application.retire", 1)]
public sealed record RetireApplication(Uuid CommandId, Uuid TenantId, Uuid ApplicationId, long ExpectedRevision,
    DateTimeOffset EffectiveAt, string Reason, Uuid? MergedIntoApplicationId = null)
    : IRequest, IApplicationInventoryWriteRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
