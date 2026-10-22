using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Records an attributable severity, ownership, due-date, scope, or root-cause change.</summary>
public sealed class ReviseFindingHandler(IAggregateExecutor executor, IAggregateReader reader,
    IDomainEventReader events, TimeProvider clock) : IRequestHandler<ReviseFinding, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(IRequestContext<ReviseFinding> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        return await RemediationCommands.ExecuteAsync(executor, reader, events, context, request.TenantId,
            request.ProgramId, request.FindingId, now, (finding, _) => finding.Revise(context.RequestId,
                request.ExpectedRevision, request.Severity, request.OwnerMemberId, request.DueOn,
                request.AffectedScope, request.RootCause, request.Reason,
                ActorReference.ForMember(actor.MemberId, actor.Display), now), ct, async token =>
                await RemediationCommands.IsActiveMemberAsync(reader, request.TenantId,
                    request.OwnerMemberId, token).ConfigureAwait(false) ? null : RemediationCommands.InactiveOwner())
            .ConfigureAwait(false);
    }
}
