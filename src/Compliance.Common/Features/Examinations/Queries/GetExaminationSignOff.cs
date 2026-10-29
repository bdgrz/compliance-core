using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Examinations.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.examinations.get-examination-sign-off", 1)]
public sealed record GetExaminationSignOff(Uuid TenantId, Uuid ProgramId, Uuid ExaminationSignOffId)
    : IRequest<ExaminationSignOffView>, IProgramReadRequest, ICallable;
