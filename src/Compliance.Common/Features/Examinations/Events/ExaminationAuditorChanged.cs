using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Contracts;

namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>The committed auditor changed examination decision.</summary>
[Discriminator("bdgrz.examinations.examination-auditor-changed", 1)]
public sealed record ExaminationAuditorChanged(Uuid TenantId, Uuid SubjectId, Uuid CommandId,
    long OriginalExpectedRevision, string AuthoredIntentSha256, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset OccurredAt, ExaminationView State)
    : DomainEvent, IExaminationSetupFact;
