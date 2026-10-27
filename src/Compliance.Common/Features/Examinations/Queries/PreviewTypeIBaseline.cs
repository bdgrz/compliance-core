using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Previews exact Type I sources and unresolved blockers before confirmation.</summary>
[Discriminator("bdgrz.examinations.preview-type-i-baseline", 1)]
public sealed record PreviewTypeIBaseline(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId)
    : IRequest<TypeIBaselinePreviewView>, IProgramReadRequest, ICallable;
