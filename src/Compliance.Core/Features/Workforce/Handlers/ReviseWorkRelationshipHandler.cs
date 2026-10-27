using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class ReviseWorkRelationshipHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<ReviseWorkRelationship>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseWorkRelationship> context,
        CancellationToken ct)
    {
        var actor = WorkforceActor.From(context);
        var request = context.Request;
        var terms = new WorkRelationshipTerms(request.WorkerType, request.LifecycleStatus,
            request.StartDate, request.EndDate, request.Department, request.ManagerPersonId,
            request.SponsorPersonId, request.EmploymentStatusReason);
        var current = await reader.HydrateAsync(
            new WorkRelationship(request.TenantId, request.RelationshipId), ct).ConfigureAwait(false);
        if (current.IsCreated)
        {
            var invalid = await WorkRelationshipReferences.ValidateAsync(reader, request.TenantId,
                current.PersonId, terms, ct).ConfigureAwait(false);
            if (invalid is not null)
                return Result.Failure(invalid);
        }
        return await executor.ExecuteAsync(
            new WorkRelationship(request.TenantId, request.RelationshipId),
            relationship => CommandFailureRequestAdapter.ToOutcome(relationship.Revise(
                request.ExpectedRevision, terms, actor, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }
}
