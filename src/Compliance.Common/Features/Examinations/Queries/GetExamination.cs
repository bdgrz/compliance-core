using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;

namespace Bdgrz.Compliance.Features.Examinations.Queries;

/// <summary>Reads current canonical setup without conflating it with the immutable baseline.</summary>
[Discriminator("bdgrz.examinations.get-examination", 1)]
public sealed record GetExamination(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId)
    : IRequest<ExaminationView>, IProgramReadRequest, ICallable;
