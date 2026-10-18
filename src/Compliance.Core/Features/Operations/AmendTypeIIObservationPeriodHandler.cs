using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

public sealed class AmendTypeIIObservationPeriodHandler(IAggregateExecutor executor,
    IAggregateReader reader, IDomainEventReader events, TimeProvider clock)
    : IRequestHandler<AmendTypeIIObservationPeriod>
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
        var reason = request.AmendmentReason?.Trim() ?? string.Empty;
        var now = clock.GetUtcNow();
        if (period.Amend(request.ExpectedRevision, start, end, reason,
                actorMemberId, actorDisplay, now) is { } amendmentFailure)
            return Result.Failure(amendmentFailure.ToRequestError());

        var bootstrap = await TypeIIObservationCalendarBootstrap.EnsureAsync(executor, reader,
            events, context, request.TenantId, request.ProgramId, ct).ConfigureAwait(false);
        if (!bootstrap.IsSuccess)
            return bootstrap;

        var calendar = await reader.HydrateAsync(new TypeIIObservationCalendar(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        var reservation = calendar.Current?.Periods.SingleOrDefault(item =>
            item.PeriodId == request.PeriodId);
        if (reservation is null)
        {
            var reserved = await executor.ExecuteAsync(new TypeIIObservationCalendar(request.TenantId,
                    request.ProgramId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                    aggregate.Reserve(current.PeriodId, current.ExaminationId, current.TypeIIPlanId,
                        current.PeriodStart, current.PeriodEnd, current.OpeningSnapshotSha256,
                        current.ActorMemberId, current.ActorDisplay, current.UpdatedAt)), context, ct)
                .ConfigureAwait(false);
            if (!reserved.IsSuccess)
                return reserved;
        }
        else if (!CalendarMatchesPeriod(reservation, current, start, end, reason,
                     actorMemberId, actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The period and program calendar disagree about the current date amendment."));

        var begun = await executor.ExecuteAsync(new TypeIIObservationCalendar(request.TenantId,
                request.ProgramId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.BeginAmendment(request.PeriodId, start, end, reason,
                    actorMemberId, actorDisplay, now)), context, ct)
            .ConfigureAwait(false);
        if (!begun.IsSuccess)
            return begun;

        var amended = await executor.ExecuteAsync(new TypeIIPeriod(request.TenantId,
                request.ProgramId, request.PeriodId), aggregate =>
                CommandFailureRequestAdapter.ToOutcome(aggregate.Amend(request.ExpectedRevision,
                    start, end, reason, actorMemberId, actorDisplay, now)),
            context, ct).ConfigureAwait(false);
        if (!amended.IsSuccess)
        {
            var cancelledAt = clock.GetUtcNow();
            var cancelled = await executor.ExecuteAsync(new TypeIIObservationCalendar(
                    request.TenantId, request.ProgramId), aggregate =>
                    CommandFailureRequestAdapter.ToOutcome(aggregate.CancelAmendment(
                        request.PeriodId, start, end, reason, actorMemberId,
                        actorDisplay, cancelledAt)), context, ct)
                .ConfigureAwait(false);
            return cancelled.IsSuccess ? amended : cancelled;
        }

        var completedAt = clock.GetUtcNow();
        return await executor.ExecuteAsync(new TypeIIObservationCalendar(request.TenantId,
                request.ProgramId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.CompleteAmendment(request.PeriodId, start, end,
                    reason, actorMemberId, actorDisplay, completedAt)), context, ct)
            .ConfigureAwait(false);
    }

    static bool CalendarMatchesPeriod(TypeIIObservationCalendarPeriodView reservation,
        TypeIIPeriodView period, DateOnly requestedStart, DateOnly requestedEnd,
        string reason, Uuid actorMemberId, string actorDisplay)
    {
        if (reservation.PendingAmendment is { } pending)
            return SameAmendment(pending, requestedStart, requestedEnd, reason,
                    actorMemberId, actorDisplay) &&
                (reservation.PeriodStart == period.PeriodStart &&
                 reservation.PeriodEnd == period.PeriodEnd ||
                 pending.PeriodStart == period.PeriodStart &&
                 pending.PeriodEnd == period.PeriodEnd);
        return reservation.PeriodStart == period.PeriodStart &&
            reservation.PeriodEnd == period.PeriodEnd;
    }

    static bool SameAmendment(TypeIIObservationCalendarAmendmentView amendment,
        DateOnly periodStart, DateOnly periodEnd, string reason, Uuid actorMemberId,
        string actorDisplay) => amendment.PeriodStart == periodStart &&
        amendment.PeriodEnd == periodEnd && amendment.Reason == reason &&
        amendment.ActorMemberId == actorMemberId && amendment.ActorDisplay == actorDisplay.Trim();
}
