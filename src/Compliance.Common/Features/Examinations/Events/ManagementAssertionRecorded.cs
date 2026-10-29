using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Models;
namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>Creates a versioned assertion and records its exact input bindings and wording.</summary>
[Discriminator("bdgrz.examinations.management-assertion-recorded", 2)]
public sealed record ManagementAssertionRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ManagementAssertionView State,
    DateTimeOffset OccurredAt) : DomainEvent;
