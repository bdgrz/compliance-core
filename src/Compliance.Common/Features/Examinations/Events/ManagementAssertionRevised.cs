using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Models;
namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>Appends a new immutable wording and input binding to a management assertion.</summary>
[Discriminator("bdgrz.examinations.management-assertion-revised", 1)]
public sealed record ManagementAssertionRevised(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ManagementAssertionView State,
    DateTimeOffset OccurredAt) : DomainEvent;
