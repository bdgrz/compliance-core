using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Persists a replay-safe batch of due expected occurrences for one active Type II period.</summary>
public sealed class GenerateDueControlOccurrencesHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<GenerateDueControlOccurrences>
{
    public ValueTask<Result> HandleAsync(IRequestContext<GenerateDueControlOccurrences> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Recurring occurrence generation requires an authorized personal HTTP actor.")));

        return executor.ExecuteAsync(new TypeIIPeriod(request.TenantId, request.ProgramId,
                request.PeriodId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.GenerateDueOccurrences(request.GenerationKey, request.ThroughDate,
                    actorMemberId, actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
