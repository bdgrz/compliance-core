using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Records the durable outcome of one exact weekly digest delivery attempt.</summary>
[Discriminator("bdgrz.work.weekly-digest.delivery-recorded", 1)]
public sealed record WeeklyWorkDigestDeliveryRecorded(
    Uuid TenantId,
    Uuid DigestId,
    Uuid MemberId,
    long Revision,
    int AttemptNumber,
    string DeliveryKey,
    string Outcome,
    string? FailureCode,
    ActorReference RecordedBy,
    DateTimeOffset AttemptedAt) : DomainEvent;
