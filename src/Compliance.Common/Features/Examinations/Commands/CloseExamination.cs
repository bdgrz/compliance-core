using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;

namespace Bdgrz.Compliance.Features.Examinations.Commands;

/// <summary>Internal reaction to an exact committed management sign-off.</summary>
[Discriminator("bdgrz.examinations.close-examination", 1)]
public sealed record CloseExamination(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId,
    ExaminationClosure Closure) : IRequest, IProgramScopedRequest;
