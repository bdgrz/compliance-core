using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Queries;
namespace Bdgrz.Compliance.Features.Controls.Handlers;

public sealed class PreviewControlImpactHandler(ControlImpactService service)
    : IRequestHandler<PreviewControlImpact, ControlImpactPreview>
{
    public ValueTask<Result<ControlImpactPreview>> HandleAsync(
        IRequestContext<PreviewControlImpact> context, CancellationToken ct) =>
        service.PreviewAsync(context.Request, ct);
}
