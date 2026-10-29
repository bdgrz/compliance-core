using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Examinations.Queries;

/// <summary>Previews exact Type I sources and unresolved blockers before confirmation.</summary>
[Discriminator("bdgrz.examinations.preview-type-i-baseline", 1)]
public sealed record PreviewTypeIBaseline(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId)
    : IRequest<TypeIBaselinePreviewView>, IProgramReadRequest, ICallable;
