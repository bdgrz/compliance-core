using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Raises a reactor-routed finding with the product defaults (medium severity, due in 30
///     days) and an optional corrective action. Replays are idempotent by finding ID.
/// </summary>
static class RoutedFinding
{
    public const int DefaultDueDays = 30;
    public const string DefaultSeverity = "medium";

    public static async ValueTask<Result> RaiseAsync<TRequest>(IAggregateExecutor executor,
        IAggregateReader reader, IDomainEventReader events,
        IRequestContext<TRequest> context, Uuid tenantId, Uuid programId, Uuid findingId,
        FindingSource source, string title, string affectedScope, Uuid ownerMemberId,
        IReadOnlyList<FindingLink> links, string? correctiveAction, ActorReference actor,
        DateTimeOffset now, CancellationToken ct)
        where TRequest : IRequestBase
    {
        var ready = await FindingStreamRouting.EnsureReadyAsync(executor, reader, events, context,
            tenantId, programId, now, ct).ConfigureAwait(false);
        if (!ready.IsSuccess)
            return ready;
        var dueOn = DateOnly.FromDateTime(now.UtcDateTime).AddDays(DefaultDueDays);
        return await executor.ExecuteAsync(new FindingAggregate(tenantId, programId, findingId, context.RequestId), finding =>
            CommandFailureRequestAdapter.ToOutcome(finding.RaiseRouted(context.RequestId, source,
                title, affectedScope, ownerMemberId, dueOn, links, correctiveAction, actor, now)),
            context, ct).ConfigureAwait(false);
    }
}
