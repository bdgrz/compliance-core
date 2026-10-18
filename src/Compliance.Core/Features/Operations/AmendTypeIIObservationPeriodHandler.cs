using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

public sealed class AmendTypeIIObservationPeriodHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<AmendTypeIIObservationPeriod>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AmendTypeIIObservationPeriod> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "A Type II period amendment requires a personal HTTP actor."));
        if (request.PeriodStart is null && request.PeriodEnd is null)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A Type II period amendment must change at least one boundary."));

        var period = await reader.HydrateAsync(new TypeIIPeriod(request.TenantId, request.ProgramId,
            request.PeriodId), ct).ConfigureAwait(false);
        if (period.Current is not { } current)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The Type II observation period was not found."));

        var start = request.PeriodStart ?? current.PeriodStart;
        var end = request.PeriodEnd ?? current.PeriodEnd;
        return await executor.ExecuteAsync(new TypeIIPeriod(request.TenantId, request.ProgramId,
                request.PeriodId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.Amend(request.ExpectedRevision, start, end, request.AmendmentReason,
                    actorMemberId, actorDisplay, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }
}
