using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Queries;
namespace Bdgrz.Compliance.Features.Controls.Handlers;

public sealed class ListControlDecisionsHandler(ControlActivationSource source)
    : IRequestHandler<ListControlDecisions, Page<ControlDecisionView>>
{
    public async ValueTask<Result<Page<ControlDecisionView>>> HandleAsync(
        IRequestContext<ListControlDecisions> context, CancellationToken ct)
    {
        var request = context.Request;
        var control = await source.LoadAsync(request.TenantId, request.ProgramId,
            request.ControlId, ct).ConfigureAwait(false);
        return control.IsSuccess
            ? ControlActivationSource.Paginate(control.Value.ReadDecisions(), request.Limit,
                request.Cursor, "control decision")
            : Result<Page<ControlDecisionView>>.Failure(control.Error);
    }
}
