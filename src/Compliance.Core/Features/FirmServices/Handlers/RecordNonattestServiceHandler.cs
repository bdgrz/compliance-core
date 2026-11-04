using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Aggregates;
using Bdgrz.Compliance.Features.FirmServices.Commands;
using Bdgrz.Compliance.Features.FirmServices.Models;
using Bdgrz.Compliance.Features.FirmServices.Services;
namespace Bdgrz.Compliance.Features.FirmServices.Handlers;

/// <summary>Invokes one service owner after current partner authority, including accepted retries.</summary>
sealed class RecordNonattestServiceHandler(IAggregateExecutor executor, FirmServicesAuthority authority, TimeProvider clock)
    : IRequestHandler<RecordNonattestService, NonattestServiceRecordView>
{
    public async ValueTask<Result<NonattestServiceRecordView>> HandleAsync(IRequestContext<RecordNonattestService> context,
        CancellationToken ct)
    {
        var actor = await authority.RequirePartnerAsync(context.Actor, "engagement_partner", ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result<NonattestServiceRecordView>.Failure(actor.Error!);
        var request = context.Request;
        var at = clock.GetUtcNow();
        return await executor.ExecuteAsync(new NonattestServiceRecord(request.TenantId, request.RecordId, request.CommandId),
            owner =>
            {
                var result = owner.Record(request, actor.Value, at);
                return result.IsSuccess ? AggregateOutcome.Commit(result) : AggregateOutcome.Discard(result);
            }, context, ct).ConfigureAwait(false);
    }
}
