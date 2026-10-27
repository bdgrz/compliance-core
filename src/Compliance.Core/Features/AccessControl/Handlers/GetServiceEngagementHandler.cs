using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class GetServiceEngagementHandler(ServiceEngagementViews views)
    : IRequestHandler<GetServiceEngagement, ServiceEngagementView>
{
    public async ValueTask<Result<ServiceEngagementView>> HandleAsync(IRequestContext<GetServiceEngagement> context,
        CancellationToken ct) => await views.GetAsync(context.Request.TenantId, context.Request.EngagementId, ct)
            .ConfigureAwait(false) is { } engagement
        ? Result<ServiceEngagementView>.Success(engagement)
        : Result<ServiceEngagementView>.Failure(new RequestError(RequestErrorKind.NotFound,
            "The client service engagement was not found."));
}
