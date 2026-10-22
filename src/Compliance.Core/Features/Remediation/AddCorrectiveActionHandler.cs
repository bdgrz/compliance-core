using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Adds owned corrective work to a finding; it enters the owner's work queue.</summary>
public sealed class AddCorrectiveActionHandler(IAggregateExecutor executor,
    IAggregateReader reader, IDomainEventReader events, TimeProvider clock) : IRequestHandler<AddCorrectiveAction, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(
        IRequestContext<AddCorrectiveAction> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        return await RemediationCommands.ExecuteAsync(executor, reader, events, context, request.TenantId,
            request.ProgramId, request.FindingId, now, (finding, _) => finding.AddAction(context.RequestId,
                request.ExpectedRevision, context.RequestId,
                request.Description, request.OwnerMemberId, request.DueOn,
                ActorReference.ForMember(actor.MemberId, actor.Display), now), ct, async token =>
                await RemediationCommands.IsActiveMemberAsync(reader, request.TenantId,
                    request.OwnerMemberId, token).ConfigureAwait(false) ? null : RemediationCommands.InactiveOwner())
            .ConfigureAwait(false);
    }
}
