using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

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
