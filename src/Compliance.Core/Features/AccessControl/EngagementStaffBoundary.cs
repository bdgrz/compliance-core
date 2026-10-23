using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

static class EngagementStaffBoundary
{
    internal static async ValueTask<Result<ServiceEngagementView>> ExecuteAsync<T>(FirmStaffViews staffViews,
        IAggregateExecutor executor, TimeProvider clock, IRequestContext<T> context, Uuid staffMemberId,
        Func<IndependenceLedger, FirmStaffMemberView, ActorReference, DateTimeOffset, Result<ServiceEngagementView>> operation,
        CancellationToken ct) where T : IRequest<ServiceEngagementView>, IIndependenceAdministrationRequest
    {
        if (await staffViews.GetAsync(staffMemberId, ct).ConfigureAwait(false) is not { IsActive: true } staff)
            return Result<ServiceEngagementView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The current active firm-staff identity was not found."));
        var result = await executor.ExecuteAsync(new IndependenceLedger(context.Request.TenantId), ledger =>
            AggregateOutcome.CommitOnSuccess(operation(ledger, staff,
                AccessGrantActor.From(context, context.Request.TenantId), clock.GetUtcNow())), context, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return result;
        var latest = await staffViews.GetAsync(staffMemberId, ct).ConfigureAwait(false);
        return latest == staff ? result : Result<ServiceEngagementView>.Failure(new RequestError(
            RequestErrorKind.Conflict, "The staff record changed. Reload before retaining another tentative proposal.",
            isTransient: true));
    }
}
