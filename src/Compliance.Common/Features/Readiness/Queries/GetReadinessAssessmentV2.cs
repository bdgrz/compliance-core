using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Readiness.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Queries;

[Discriminator("bdgrz.readiness.assessment.get", 2)]
public sealed record GetReadinessAssessmentV2(Uuid TenantId, Uuid ProgramId, Uuid AssessmentId)
    : IRequest<ReadinessAssessmentResult>, IProgramReadRequest, ICallable;
