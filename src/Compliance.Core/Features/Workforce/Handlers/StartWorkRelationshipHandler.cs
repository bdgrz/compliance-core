using Cntryl.Portia;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Services;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class StartWorkRelationshipHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<StartWorkRelationship>
{
    public ValueTask<Result> HandleAsync(IRequestContext<StartWorkRelationship> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = WorkforceActor.From(context);
        return executor.ExecuteAsync(new WorkRelationship(request.TenantId, request.RelationshipId),
            relationship => CommandFailureRequestAdapter.ToOutcome(relationship.Start(
                request.ExpectedRevision, request.StartDate, actor, clock.GetUtcNow())), context, ct);
    }
}
