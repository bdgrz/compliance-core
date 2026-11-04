using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Models;

namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>Freezes the current assertion and exact validated examination package under a named signer.</summary>
[Discriminator("bdgrz.examinations.management-assertion-signed", 1)]
public sealed record ManagementAssertionSigned(Uuid TenantId, Uuid SubjectId, long Revision,
    Uuid ActorMemberId, ManagementAssertionView State, DateTimeOffset OccurredAt) : DomainEvent;
