using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Contracts;

namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>The committed closed examination decision.</summary>
[Discriminator("bdgrz.examinations.examination-closed", 1)]
public sealed record ExaminationClosed(Uuid TenantId, Uuid SubjectId, Uuid CommandId,
    long OriginalExpectedRevision, string AuthoredIntentSha256, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset OccurredAt, ExaminationView State)
    : DomainEvent, IExaminationSetupFact;
