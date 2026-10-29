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
