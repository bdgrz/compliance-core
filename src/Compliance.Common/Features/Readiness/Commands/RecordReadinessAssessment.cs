using Bdgrz.Compliance.Features.Readiness.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Commands;

/// <summary>Computes and records one immutable assessment directly, on request or for its daily identity.</summary>
[Discriminator("bdgrz.readiness.assessment.record", 1)]
public sealed record RecordReadinessAssessment(Uuid AssessmentId, Uuid TenantId, Uuid ProgramId,
    DateTimeOffset? AsOf = null, DateOnly? ScheduledDay = null) : IRequest<ReadinessAssessmentResult>, ICallable;
