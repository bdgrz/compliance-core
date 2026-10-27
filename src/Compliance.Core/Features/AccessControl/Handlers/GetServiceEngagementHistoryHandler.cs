using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetServiceEngagementHistoryHandler(ServiceEngagementViews views)
    : IRequestHandler<GetServiceEngagementHistory, IReadOnlyList<ServiceEngagementView>>
{
    public async ValueTask<Result<IReadOnlyList<ServiceEngagementView>>> HandleAsync(
        IRequestContext<GetServiceEngagementHistory> context, CancellationToken ct) =>
        await views.HistoryAsync(context.Request.TenantId, context.Request.EngagementId, ct).ConfigureAwait(false)
            is { } history
            ? Result<IReadOnlyList<ServiceEngagementView>>.Success(history)
            : Result<IReadOnlyList<ServiceEngagementView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The client service engagement was not found."));
}
