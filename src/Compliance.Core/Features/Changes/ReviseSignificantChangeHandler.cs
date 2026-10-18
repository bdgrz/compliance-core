using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

public sealed class ReviseSignificantChangeHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviseSignificantChange>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseSignificantChange> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var (actorMemberId, actorDisplay) = ClientServiceActor.Snapshot(context);
        return executor.ExecuteAsync(new SignificantChange(request.TenantId, request.ProgramId,
                request.ChangeId), change => CommandFailureRequestAdapter.ToOutcome(
                change.Revise(request.ExpectedRevision, request.Content, request.ChangeReason,
                    actorMemberId, actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
