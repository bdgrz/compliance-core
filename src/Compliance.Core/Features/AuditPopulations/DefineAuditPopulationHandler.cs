using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

public sealed class DefineAuditPopulationHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<DefineAuditPopulation>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<DefineAuditPopulation> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditPopulationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Defining an audit population requires a personal HTTP actor."));

        if (request.PeriodId == Uuid.Empty || request.PeriodCloseId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Population definition requires a Type II period and exact close snapshot identity."));

        var period = await reader.HydrateAsync(new TypeIIPeriod(request.TenantId,
            request.ProgramId, request.PeriodId), ct).ConfigureAwait(false);
        if (period.Current is not { } current)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The Type II observation period was not found."));
        if (current.Status != "closed" || current.CloseSnapshots.Count == 0 ||
            current.CloseSnapshots[^1].CloseSnapshotId != request.PeriodCloseId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Population definition must bind the exact current frozen Type II close snapshot."));

        return await executor.ExecuteAsync(new AuditPopulation(request.TenantId, request.ProgramId,
                request.DefinitionId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.Define(request.DefinitionId, request.Definition, current,
                    actorMemberId, actorDisplay, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }
}
