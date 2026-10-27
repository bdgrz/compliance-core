using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Commands;

/// <summary>Records the deliver weekly work digest intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.work.deliver-weekly-work-digest", 3)]
public sealed record DeliverWeeklyWorkDigest(Uuid CommandId, Uuid TenantId, Uuid MemberId, Uuid DigestId,
    Uuid ScheduleEventId, long ExpectedRevision, int AttemptNumber, string DeliveryKey) : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
