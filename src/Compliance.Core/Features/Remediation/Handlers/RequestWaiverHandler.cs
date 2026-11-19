using Cntryl.Portia;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Hosting;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

public sealed class RequestWaiverHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RequestWaiver, PortiaCreateOutcome<WaiverRegistration>>
{
    public async ValueTask<Result<PortiaCreateOutcome<WaiverRegistration>>> HandleAsync(
        IRequestContext<RequestWaiver> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = WaiverActors.Member(context.Actor, request.TenantId);
        if (!actor.IsSuccess)
            return Result<PortiaCreateOutcome<WaiverRegistration>>.Failure(actor.Error);
        var now = clock.GetUtcNow();
        var created = false;
        var result = await executor.ExecuteAsync(new Waiver(request.TenantId, request.WaiverId),
            waiver =>
            {
                created = waiver.Revision == 0;
                return AggregateOutcome.CommitOnSuccess(waiver.Request(request, actor.Value, now));
            }, context, ct).ConfigureAwait(false);
        return PortiaCreateOutcomes.From(result, created);
    }
}
