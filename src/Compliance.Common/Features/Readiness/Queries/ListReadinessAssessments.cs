using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Readiness.Models;
namespace Bdgrz.Compliance.Features.Readiness.Queries;

[Discriminator("bdgrz.readiness.assessments.list", 1)]
public sealed record ListReadinessAssessments(Uuid TenantId, Uuid ProgramId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<ReadinessAssessmentSummaryView>>, IProgramReadRequest, ICallable;
