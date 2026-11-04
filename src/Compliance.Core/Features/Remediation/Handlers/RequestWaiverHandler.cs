using Cntryl.Portia;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Services;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

public sealed class RequestWaiverHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RequestWaiver, WaiverRegistration>
{
    public async ValueTask<Result<WaiverRegistration>> HandleAsync(IRequestContext<RequestWaiver> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = WaiverActors.Member(context.Actor, request.TenantId);
        if (!actor.IsSuccess)
            return Result<WaiverRegistration>.Failure(actor.Error);
        var now = clock.GetUtcNow();
        return await executor.ExecuteAsync(new Waiver(request.TenantId, request.WaiverId),
            waiver => AggregateOutcome.CommitOnSuccess(waiver.Request(request, actor.Value, now)), context, ct)
            .ConfigureAwait(false);
    }
}
