using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Queries;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Handlers;

public sealed class ListTypeIEntryDecisionsV2Handler(TypeIEntryDecisionReader decisions)
    : IRequestHandler<ListTypeIEntryDecisionsV2, Page<TypeIEntryDecisionRecord>>
{
    public async ValueTask<Result<Page<TypeIEntryDecisionRecord>>> HandleAsync(
        IRequestContext<ListTypeIEntryDecisionsV2> context, CancellationToken ct)
    {
        var request = context.Request;
        var rows = await decisions.ListAsync(request.TenantId, request.ProgramId, ct).ConfigureAwait(false);
        return ControlActivationSource.Paginate(rows, request.Limit, request.Cursor, "Type I entry decisions");
    }
}
