using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class ListAccessGrantsHandler(IAccessGrantDirectory grants)
    : IRequestHandler<ListAccessGrants, AccessGrantSetView>
{
    public async ValueTask<Result<AccessGrantSetView>> HandleAsync(
        IRequestContext<ListAccessGrants> context, CancellationToken ct) =>
        Result<AccessGrantSetView>.Success(await grants.ListAsync(context.Request.TenantId, ct)
            .ConfigureAwait(false));
}
