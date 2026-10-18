using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

public sealed class RecordSignificantChangeHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RecordSignificantChange>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordSignificantChange> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var (actorMemberId, actorDisplay) = ClientServiceActor.Snapshot(context);
        return executor.ExecuteAsync(new SignificantChange(request.TenantId, request.ProgramId,
                request.ChangeId), change => CommandFailureRequestAdapter.ToOutcome(
                change.Record(request.Content, actorMemberId, actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
