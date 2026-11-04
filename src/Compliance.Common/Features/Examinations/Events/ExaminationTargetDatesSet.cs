using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Contracts;

namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>The committed target dates set examination decision.</summary>
[Discriminator("bdgrz.examinations.examination-target-dates-set", 1)]
public sealed record ExaminationTargetDatesSet(Uuid TenantId, Uuid SubjectId, Uuid CommandId,
    long OriginalExpectedRevision, string AuthoredIntentSha256, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset OccurredAt, ExaminationView State)
    : DomainEvent, IExaminationSetupFact;
