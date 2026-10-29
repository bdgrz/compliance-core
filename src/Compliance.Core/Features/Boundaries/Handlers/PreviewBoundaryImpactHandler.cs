using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries.Queries;
namespace Bdgrz.Compliance.Features.Boundaries.Handlers;

public sealed class PreviewBoundaryImpactHandler(BoundaryImpactService service)
    : IRequestHandler<PreviewBoundaryImpact, BoundaryImpactPreview>
{
    public ValueTask<Result<BoundaryImpactPreview>> HandleAsync(
        IRequestContext<PreviewBoundaryImpact> context, CancellationToken ct) =>
        service.PreviewAsync(context.Request, ct);
}
