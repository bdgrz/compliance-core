using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Queries;
namespace Bdgrz.Compliance.Features.Controls.Handlers;

public sealed class GetControlDraftHandler(ControlDraftReadConsistency consistency)
    : IRequestHandler<GetControlDraft, ControlDraftView>
{
    public ValueTask<Result<ControlDraftView>> HandleAsync(
        IRequestContext<GetControlDraft> context, CancellationToken ct) =>
        consistency.GetAsync(context.Request.TenantId, context.Request.ProgramId,
            context.Request.ControlId, context.Request.MinimumRevision, ct);
}
