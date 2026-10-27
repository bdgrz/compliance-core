using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Queries;

[Discriminator("bdgrz.boundary.impact.preview", 1)]
public sealed record PreviewBoundaryImpact(Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, long ExpectedRevision)
    : IRequest<BoundaryImpactPreview>, IBoundaryProgramReadRequest, ICallable;
