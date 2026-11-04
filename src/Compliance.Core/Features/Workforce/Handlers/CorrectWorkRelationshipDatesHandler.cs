using Cntryl.Portia;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Services;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class CorrectWorkRelationshipDatesHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<CorrectWorkRelationshipDates>
{
    public ValueTask<Result> HandleAsync(IRequestContext<CorrectWorkRelationshipDates> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = WorkforceActor.From(context);
        return executor.ExecuteAsync(new WorkRelationship(request.TenantId, request.RelationshipId),
            relationship => CommandFailureRequestAdapter.ToOutcome(relationship.CorrectDates(
                request.ExpectedRevision, request.StartDate, request.EndDate, request.Reason, actor, clock.GetUtcNow())), context, ct);
    }
}
