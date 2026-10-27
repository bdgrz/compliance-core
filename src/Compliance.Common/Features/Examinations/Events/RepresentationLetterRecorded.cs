using Bdgrz.Compliance.Features.Examinations.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>Records an externally sourced representation letter and its required signers.</summary>
[Discriminator("bdgrz.examinations.representation-letter-recorded", 2)]
public sealed record RepresentationLetterRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ManagementRepresentationLetterView State,
    DateTimeOffset OccurredAt) : DomainEvent;
