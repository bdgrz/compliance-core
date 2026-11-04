using Cntryl.Portia;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Bdgrz.Compliance.Features.Workforce.Services;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class ChangeSponsorHandler(IAggregateExecutor executor, IAggregateReader reader,
    IWorkforceObservationDirectoryReader roster, WorkforceObservationReadConsistency consistency, TimeProvider clock)
    : IRequestHandler<ChangeSponsor>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ChangeSponsor> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = WorkforceActor.From(context);
        var current = await reader.HydrateAsync(new WorkRelationship(request.TenantId, request.RelationshipId), ct)
            .ConfigureAwait(false);
        if (current.IsCreated && request.SponsorPersonId != Uuid.Empty)
        {
            var sponsor = await reader.HydrateAsync(new Person(request.TenantId, request.SponsorPersonId), ct)
                .ConfigureAwait(false);
            if (!sponsor.IsCreated)
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "The sponsor must be a recorded person of this organization."));
            var ready = await consistency.EnsureCaughtUpAsync(request.TenantId, ct).ConfigureAwait(false);
            if (!ready.IsSuccess)
                return ready;
            var active = await roster.HasActiveInternalRelationshipAsync(request.TenantId, request.SponsorPersonId, ct)
                .ConfigureAwait(false);
            if (!active)
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "The sponsor must have an active employee or contractor relationship."));
        }
        return await executor.ExecuteAsync(new WorkRelationship(request.TenantId, request.RelationshipId),
            relationship => CommandFailureRequestAdapter.ToOutcome(relationship.ChangeSponsor(
                request.ExpectedRevision, request.SponsorPersonId, actor, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }
}
