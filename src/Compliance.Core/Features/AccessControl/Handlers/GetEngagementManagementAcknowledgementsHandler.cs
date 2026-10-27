using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Reads the materialized management acknowledgements of one engagement from their own owners.</summary>
public sealed class GetEngagementManagementAcknowledgementsHandler(EngagementPersonalDecisionViews views)
    : IRequestHandler<GetEngagementManagementAcknowledgements, IReadOnlyList<EngagementManagementAcknowledgementView>>
{
    public async ValueTask<Result<IReadOnlyList<EngagementManagementAcknowledgementView>>> HandleAsync(
        IRequestContext<GetEngagementManagementAcknowledgements> context, CancellationToken ct) =>
        Result<IReadOnlyList<EngagementManagementAcknowledgementView>>.Success(
            await views.ManagementAcknowledgementsAsync(context.Request.TenantId, context.Request.EngagementId, ct)
                .ConfigureAwait(false));
}
