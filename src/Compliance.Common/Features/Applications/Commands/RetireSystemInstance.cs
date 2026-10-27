using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Retires a system instance; <paramref name="CommandId" /> is the logical identity of the operation.</summary>
[Discriminator("bdgrz.system_instance.retire", 1)]
public sealed record RetireSystemInstance(Uuid CommandId, Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long ExpectedRevision, DateTimeOffset EffectiveAt, string Reason)
    : IRequest, IApplicationInventoryWriteRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
