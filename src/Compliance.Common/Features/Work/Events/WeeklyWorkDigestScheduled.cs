using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Events;

/// <summary>Schedules one member's stable weekly digest identity for delivery processing.</summary>
[Discriminator("bdgrz.work.weekly-digest.scheduled", 1)]
public sealed record WeeklyWorkDigestScheduled(
    Uuid TenantId,
    Uuid DigestId,
    Uuid MemberId,
    DateOnly WeekStarting,
    string ScheduleKey,
    ActorReference ScheduledBy,
    DateTimeOffset ScheduledAt) : DomainEvent;
