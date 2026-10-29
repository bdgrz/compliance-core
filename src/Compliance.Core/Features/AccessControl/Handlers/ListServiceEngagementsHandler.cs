using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Queries;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class ListServiceEngagementsHandler(ServiceEngagementViews views)
    : IRequestHandler<ListServiceEngagements, IReadOnlyList<ServiceEngagementView>>
{
    public async ValueTask<Result<IReadOnlyList<ServiceEngagementView>>> HandleAsync(
        IRequestContext<ListServiceEngagements> context, CancellationToken ct) =>
        Result<IReadOnlyList<ServiceEngagementView>>.Success(
            await views.ListAsync(context.Request.TenantId, ct).ConfigureAwait(false));
}
