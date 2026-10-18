using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

public sealed class AssessSignificantChangeHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<AssessSignificantChange>
{
    public ValueTask<Result> HandleAsync(IRequestContext<AssessSignificantChange> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var (actorMemberId, actorDisplay) = ClientServiceActor.Snapshot(context);
        return executor.ExecuteAsync(new SignificantChange(request.TenantId, request.ProgramId,
                request.ChangeId), change => CommandFailureRequestAdapter.ToOutcome(
                change.Assess(request.ExpectedRevision, request.Content, actorMemberId,
                    actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
