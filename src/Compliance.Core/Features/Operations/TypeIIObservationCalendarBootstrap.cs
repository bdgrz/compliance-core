using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Seeds the authoritative program calendar from committed Type II period streams.</summary>
static class TypeIIObservationCalendarBootstrap
{
    const int MaximumPeriods = 10_000;

    public static async ValueTask<Result> EnsureAsync<TRequest>(IAggregateExecutor executor,
        IAggregateReader reader, IDomainEventReader events, IRequestContext<TRequest> context,
        Uuid tenantId, Uuid programId, CancellationToken ct)
        where TRequest : IRequestBase
    {
        var current = await reader.HydrateAsync(new TypeIIObservationCalendar(tenantId, programId), ct)
            .ConfigureAwait(false);
        if (current.Revision > 0)
            return Result.Success;

        var source = await ReadPeriodsAsync(events, tenantId, programId, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result.Failure(source.Error!);
        if (source.Value!.Count == 0)
            return Result.Success;

        return await executor.ExecuteAsync(new TypeIIObservationCalendar(tenantId, programId), calendar =>
        {
            foreach (var period in source.Value)
            {
                var state = period.State;
                var failure = calendar.Reserve(state.PeriodId, state.ExaminationId,
                    state.TypeIIPlanId, state.PeriodStart, state.PeriodEnd,
                    state.OpeningSnapshotSha256, period.StartedByMemberId,
                    period.StartedByDisplay, period.StartedAt, isActive: state.Status != "closed");
                if (failure is not null)
                    return CommandFailureRequestAdapter.ToOutcome(failure);
            }
            return AggregateOutcome.Commit(Result.Success);
        }, context, ct).ConfigureAwait(false);
    }

    static async ValueTask<Result<IReadOnlyList<Seed>>> ReadPeriodsAsync(IDomainEventReader events,
        Uuid tenantId, Uuid programId, CancellationToken ct)
    {
        var streams = new Dictionary<Uuid, Seed>();
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString(), "type-ii-periods", null);
        await foreach (var record in events.ReadAsync(pattern, EventCursor.Start, ct)
                           .WithCancellation(ct).ConfigureAwait(false))
        {
            if (record.Stream.Realm != tenantId.ToString() ||
                record.Stream.Area != "type-ii-periods")
                return SourceConflict("A Type II period was read from an unexpected event stream.");

            switch (record.Event)
            {
                case ObservationPeriodStarted started when started.TenantId == tenantId &&
                    started.State.ProgramId == programId:
                    if (started.SubjectId != started.State.PeriodId ||
                        started.State.TenantId != tenantId ||
                        started.State.PeriodId.ToString() != record.Stream.Resource ||
                        started.ActorMemberId is not { } actor || actor == Uuid.Empty ||
                        string.IsNullOrWhiteSpace(started.State.ActorDisplay))
                        return SourceConflict("A Type II period opening event has inconsistent identity.");
                    streams[started.SubjectId] = new Seed(started.State, actor,
                        started.State.ActorDisplay, started.OccurredAt);
                    break;

                case ObservationPeriodAmended amended when amended.TenantId == tenantId &&
                    amended.State.ProgramId == programId:
                    if (amended.SubjectId != amended.State.PeriodId ||
                        amended.State.TenantId != tenantId ||
                        amended.State.PeriodId.ToString() != record.Stream.Resource ||
                        !streams.TryGetValue(amended.SubjectId, out var prior) ||
                        !SameOpening(prior.State, amended.State))
                        return SourceConflict("A Type II period amendment has no matching immutable opening.");
                    streams[amended.SubjectId] = prior with { State = amended.State };
                    break;

                case ObservationPeriodCloseFrozen closed when closed.TenantId == tenantId &&
                    closed.State.ProgramId == programId:
                    if (closed.SubjectId != closed.State.PeriodId ||
                        closed.State.TenantId != tenantId || closed.State.Status != "closed" ||
                        closed.State.PeriodId.ToString() != record.Stream.Resource ||
                        !streams.TryGetValue(closed.SubjectId, out var closedPrior) ||
                        !SameOpening(closedPrior.State, closed.State))
                        return SourceConflict("A Type II close event has no matching immutable opening.");
                    streams[closed.SubjectId] = closedPrior with { State = closed.State };
                    break;

                case TypeIIPeriodCloseAmended amendedClose when amendedClose.TenantId == tenantId &&
                    amendedClose.State.ProgramId == programId:
                    if (amendedClose.SubjectId != amendedClose.State.PeriodId ||
                        amendedClose.State.TenantId != tenantId ||
                        amendedClose.State.Status != "closed" ||
                        amendedClose.State.PeriodId.ToString() != record.Stream.Resource ||
                        !streams.TryGetValue(amendedClose.SubjectId, out var amendedPrior) ||
                        !SameOpening(amendedPrior.State, amendedClose.State))
                        return SourceConflict("A Type II close amendment has no matching immutable opening.");
                    streams[amendedClose.SubjectId] = amendedPrior with { State = amendedClose.State };
                    break;
            }

            if (streams.Count > MaximumPeriods)
                return SourceConflict("The program exceeds the bounded Type II period calendar capacity.");
        }

        var periods = streams.Values.OrderBy(static item => item.State.PeriodStart)
            .ThenBy(static item => item.State.PeriodId.ToString(), StringComparer.Ordinal).ToArray();
        return Result<IReadOnlyList<Seed>>.Success(Array.AsReadOnly(periods));
    }

    static bool SameOpening(TypeIIPeriodView left, TypeIIPeriodView right) =>
        left.TenantId == right.TenantId && left.ProgramId == right.ProgramId &&
        left.PeriodId == right.PeriodId && left.ExaminationId == right.ExaminationId &&
        left.TypeIIPlanId == right.TypeIIPlanId &&
        left.TypeIIPlanVersion == right.TypeIIPlanVersion &&
        left.TypeIIPlanContentSha256 == right.TypeIIPlanContentSha256 &&
        left.OpeningSnapshotId == right.OpeningSnapshotId &&
        StringComparer.OrdinalIgnoreCase.Equals(left.OpeningSnapshotSha256,
            right.OpeningSnapshotSha256);

    static Result<IReadOnlyList<Seed>> SourceConflict(string message) =>
        Result<IReadOnlyList<Seed>>.Failure(new RequestError(RequestErrorKind.Conflict, message));

    sealed record Seed(TypeIIPeriodView State, Uuid StartedByMemberId,
        string StartedByDisplay, DateTimeOffset StartedAt);
}
