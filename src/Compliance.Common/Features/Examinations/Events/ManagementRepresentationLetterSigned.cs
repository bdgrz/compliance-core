using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>Retains one named executive's exact signed representation-letter artifact.</summary>
[Discriminator("bdgrz.examinations.management-representation-letter-signed", 1)]
public sealed record ManagementRepresentationLetterSigned(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ManagementRepresentationLetterView State,
    DateTimeOffset OccurredAt) : DomainEvent;
