using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Retains one named executive's exact signed representation-letter artifact.</summary>
[Discriminator("bdgrz.examinations.management-representation-letter-signed", 1)]
public sealed record ManagementRepresentationLetterSigned(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ManagementRepresentationLetterView State,
    DateTimeOffset OccurredAt) : DomainEvent;
