using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries.Queries;

[Discriminator("bdgrz.boundary.impact.preview", 1)]
public sealed record PreviewBoundaryImpact(Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, long ExpectedRevision)
    : IRequest<BoundaryImpactPreview>, IBoundaryProgramReadRequest, ICallable;
