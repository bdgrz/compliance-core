using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Proposes carry-forward choices against a closed Type II period and its auditor outcome.</summary>
public sealed class ProposeProgramRolloverHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<ProposeProgramRollover>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ProposeProgramRollover> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "A program rollover proposal requires a personal HTTP actor."));
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.RolloverId == Uuid.Empty || request.Content is null ||
            request.Content.SourcePeriodId == Uuid.Empty || request.Content.AuditOutcomeId == Uuid.Empty)
            return Invalid("A rollover proposal requires exact completed-period and auditor-outcome identities.");

        var sourcePeriod = await reader.HydrateAsync(new TypeIIPeriod(request.TenantId,
            request.ProgramId, request.Content.SourcePeriodId), ct).ConfigureAwait(false);
        if (sourcePeriod.Current is not { } periodView)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The source Type II period was not found in this program."));
        if (periodView.Status != "closed" || periodView.CloseSnapshots.Count == 0)
            return Conflict("A rollover can be proposed only from a frozen, closed Type II period.");

        var outcome = await reader.HydrateAsync(new AuditOutcome(request.TenantId,
            request.ProgramId, request.Content.AuditOutcomeId), ct).ConfigureAwait(false);
        if (outcome.Current is not { } outcomeView)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The auditor outcome was not found in this program."));

        var now = clock.GetUtcNow();
        var latestSnapshot = periodView.CloseSnapshots[^1];
        if (now == default || now < latestSnapshot.RecordedAt || now < outcomeView.RecordedAt ||
            outcomeView.RecordedAt < latestSnapshot.RecordedAt ||
            outcomeView.Content.IssuedOn > DateOnly.FromDateTime(now.UtcDateTime))
            return Conflict("A rollover requires a completed close and an auditor outcome recorded before the proposal.");

        return await executor.ExecuteAsync(new ProgramRollover(request.TenantId,
                request.ProgramId, request.RolloverId), aggregate =>
                CommandFailureRequestAdapter.ToOutcome(aggregate.Propose(request.Content,
                    periodView, outcomeView, actorMemberId, actorDisplay, now)), context, ct)
            .ConfigureAwait(false);
    }

    static Result Invalid(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Validation, message));

    static Result Conflict(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
