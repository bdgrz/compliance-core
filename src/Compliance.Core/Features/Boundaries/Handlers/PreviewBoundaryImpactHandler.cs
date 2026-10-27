using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries.Queries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries.Handlers;

public sealed class PreviewBoundaryImpactHandler(BoundaryImpactService service)
    : IRequestHandler<PreviewBoundaryImpact, BoundaryImpactPreview>
{
    public ValueTask<Result<BoundaryImpactPreview>> HandleAsync(
        IRequestContext<PreviewBoundaryImpact> context, CancellationToken ct) =>
        service.PreviewAsync(context.Request, ct);
}
