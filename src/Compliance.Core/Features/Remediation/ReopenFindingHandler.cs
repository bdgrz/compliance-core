using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

public sealed class ReopenFindingHandler(IAggregateExecutor executor, IAggregateReader reader,
    IDomainEventReader events, TimeProvider clock)
    : IRequestHandler<ReopenFinding, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(IRequestContext<ReopenFinding> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        return await RemediationCommands.ExecuteAsync(executor, reader, events, context, request.TenantId,
            request.ProgramId, request.FindingId, now, (finding, _) => finding.Reopen(context.RequestId,
                request.ExpectedRevision, request.Reason,
                ActorReference.ForMember(actor.MemberId, actor.Display), now), ct)
            .ConfigureAwait(false);
    }
}
