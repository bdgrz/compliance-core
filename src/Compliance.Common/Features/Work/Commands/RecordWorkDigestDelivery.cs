using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Records the record work digest delivery intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.work.record-work-digest-delivery", 3)]
public sealed record RecordWorkDigestDelivery(Uuid CommandId, Uuid TenantId, Uuid DigestId,
    Uuid DeliveryRequestEventId, long ExpectedRevision, int AttemptNumber, string DeliveryKey,
    string Outcome, DateTimeOffset AttemptedAt, string? FailureCode) : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
