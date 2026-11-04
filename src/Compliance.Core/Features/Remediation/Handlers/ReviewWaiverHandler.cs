using Cntryl.Portia;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Services;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

public sealed class ReviewWaiverHandler(IAggregateExecutor executor, TimeProvider clock) : IRequestHandler<ReviewWaiver>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviewWaiver> context, CancellationToken ct)
    {
        if (WaiverActors.PersonalReviewError(context) is { } transportError)
            return transportError;
        var request = context.Request;
        var actor = WaiverActors.Member(context.Actor, request.TenantId);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error);
        var now = clock.GetUtcNow();
        return await executor.ExecuteAsync(new Waiver(request.TenantId, request.WaiverId),
            waiver => AggregateOutcome.CommitOnSuccess(waiver.Review(request, actor.Value, now)), context, ct)
            .ConfigureAwait(false);
    }
}
