using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Readiness.Models;
namespace Bdgrz.Compliance.Features.Readiness.Queries;

[Discriminator("bdgrz.readiness.assessment.get", 1)]
public sealed record GetReadinessAssessment(Uuid TenantId, Uuid ProgramId, Uuid AssessmentId)
    : IRequest<ReadinessAssessmentView>, IProgramReadRequest, ICallable;
