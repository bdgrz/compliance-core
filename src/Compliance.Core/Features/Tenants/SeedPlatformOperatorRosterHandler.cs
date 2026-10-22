using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class SeedPlatformOperatorRosterHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<SeedPlatformOperatorRoster>
{
    public ValueTask<Result> HandleAsync(IRequestContext<SeedPlatformOperatorRoster> context, CancellationToken ct) =>
        executor.ExecuteAsync(new PlatformOperatorRoster(context.Request.RosterId),
            roster => AggregateOutcome.CommitOnSuccess(roster.Seed(context.Request.RosterId, context.Request.UserIds,
                RbacOperationActor.From(context.Actor), clock.GetUtcNow())), context, ct);
}
