using Bdgrz.Compliance.Features.Examinations.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>Records an independent management decision on one exact assertion version.</summary>
[Discriminator("bdgrz.examinations.management-assertion-approved", 1)]
public sealed record ManagementAssertionApproved(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ManagementAssertionView State,
    DateTimeOffset OccurredAt) : DomainEvent;
