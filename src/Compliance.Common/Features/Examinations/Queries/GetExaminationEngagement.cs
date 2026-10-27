using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Examinations.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.examinations.get-examination-engagement", 1)]
public sealed record GetExaminationEngagement(Uuid TenantId, Uuid ProgramId, Uuid ExaminationEngagementId)
    : IRequest<ExaminationEngagementView>, IProgramReadRequest, ICallable;
