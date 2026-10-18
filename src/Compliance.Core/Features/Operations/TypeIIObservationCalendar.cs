using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Owns program-wide Type II period reservations and their non-overlap invariant.</summary>
public sealed class TypeIIObservationCalendar : Aggregate
{
    const int MaximumPeriods = 10_000;
    const int MaximumEventBytes = 32 * 1024;
    const int MaximumActorDisplayLength = 200;
    const int MaximumReasonLength = 2000;

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly List<TypeIIObservationCalendarPeriodView> _periods = [];
    TypeIIObservationCalendarView? _current;

    public long Revision => _current?.Revision ?? 0;
    public TypeIIObservationCalendarView? Current => _current;

    public TypeIIObservationCalendar(Uuid tenantId, Uuid programId)
        : base(programId, new EventStreamAddress(tenantId.ToString(),
            "type-ii-observation-calendars", programId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<ObservationPeriodReserved>(Apply);
        On<ObservationPeriodAmendmentReserved>(Apply);
        On<ObservationPeriodAmendmentCompleted>(Apply);
        On<ObservationPeriodAmendmentCancelled>(Apply);
        On<ObservationPeriodReservationReleased>(Apply);
    }

    public CommandFailure? Reserve(Uuid periodId, Uuid examinationId, Uuid planId,
        DateOnly periodStart, DateOnly periodEnd, string openingSnapshotSha256,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset reservedAt,
        bool isActive = true)
    {
        if (!ValidIdentity() || periodId == Uuid.Empty || examinationId == Uuid.Empty ||
            planId == Uuid.Empty || !ValidRange(periodStart, periodEnd) ||
            !Sha256(openingSnapshotSha256) || actorMemberId == Uuid.Empty ||
            !Bounded(actorDisplay, 1, MaximumActorDisplayLength) || reservedAt == default)
            return CommandFailure.InvalidContent(
                "An observation-period reservation requires exact identities, dates, snapshot, and actor.");

        if (_periods.FirstOrDefault(item => item.PeriodId == periodId) is { } existing)
            return existing.ExaminationId == examinationId && existing.PlanId == planId &&
                   existing.PeriodStart == periodStart && existing.PeriodEnd == periodEnd &&
                   HashEquals(existing.OpeningSnapshotSha256, openingSnapshotSha256) &&
                   existing.ReservedByMemberId == actorMemberId &&
                   existing.ReservedByDisplay == actorDisplay.Trim() &&
                   existing.IsActive == isActive
                ? null
                : CommandFailure.StateConflict(
                    "An observation-period identity cannot be reused with different opening intent.");
        if (_periods.Count >= MaximumPeriods)
            return CommandFailure.StateConflict("The program reached its observation-period limit.");
        if (isActive && ConflictsWith(periodStart, periodEnd, Uuid.Empty))
            return CommandFailure.StateConflict(
                "A program cannot reserve overlapping Type II observation periods.");

        var period = new TypeIIObservationCalendarPeriodView(periodId, examinationId, planId,
            periodStart, periodEnd, openingSnapshotSha256.ToLowerInvariant(), actorMemberId,
            actorDisplay.Trim(), reservedAt, reservedAt, isActive);
        var revision = Revision + 1;
        var ev = new ObservationPeriodReserved(_tenantId, _programId, revision, actorMemberId,
            period, reservedAt);
        if (JsonSerializer.SerializeToUtf8Bytes(ev).Length > MaximumEventBytes)
            return CommandFailure.InvalidContent(
                "The observation-period reservation exceeds the bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    public CommandFailure? BeginAmendment(Uuid periodId, DateOnly periodStart, DateOnly periodEnd,
        string reason, Uuid actorMemberId, string actorDisplay, DateTimeOffset requestedAt)
    {
        reason = Clean(reason);
        actorDisplay = Clean(actorDisplay);
        if (!ValidIdentity() || periodId == Uuid.Empty || !ValidRange(periodStart, periodEnd) ||
            !Bounded(reason, 1, MaximumReasonLength) || actorMemberId == Uuid.Empty ||
            !Bounded(actorDisplay, 1, MaximumActorDisplayLength) || requestedAt == default)
            return CommandFailure.InvalidContent(
                "An observation-period date amendment requires valid dates, rationale, and actor.");
        var index = _periods.FindIndex(item => item.PeriodId == periodId);
        if (index < 0)
            return CommandFailure.MissingRecord("The observation-period reservation was not found.");
        var period = _periods[index];
        if (!period.IsActive)
            return CommandFailure.StateConflict("Only an active observation period can be amended.");
        if (period.PendingAmendment is { } pending)
            return SameAmendment(pending, periodStart, periodEnd, reason, actorMemberId, actorDisplay)
                ? null
                : CommandFailure.StateConflict(
                    "The observation period already has a different date amendment in progress.");
        if (period.LastAmendment is { } last && SameAmendment(last, periodStart,
                periodEnd, reason, actorMemberId, actorDisplay))
            return null;
        if (requestedAt < period.UpdatedAt ||
            periodStart == period.PeriodStart && periodEnd == period.PeriodEnd)
            return CommandFailure.InvalidContent(
                "An observation-period amendment must change dates after the current reservation.");
        if (ConflictsWith(periodStart, periodEnd, periodId))
            return CommandFailure.StateConflict(
                "A program cannot amend a Type II period into another reserved date range.");

        var amendment = new TypeIIObservationCalendarAmendmentView(periodStart, periodEnd,
            reason, actorMemberId, actorDisplay, requestedAt, null);
        var ev = new ObservationPeriodAmendmentReserved(_tenantId, _programId,
            Revision + 1, actorMemberId, periodId, amendment, requestedAt);
        if (JsonSerializer.SerializeToUtf8Bytes(ev).Length > MaximumEventBytes)
            return CommandFailure.InvalidContent(
                "The observation-period amendment exceeds the bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    public CommandFailure? CompleteAmendment(Uuid periodId, DateOnly periodStart,
        DateOnly periodEnd, string reason, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset completedAt)
    {
        reason = Clean(reason);
        actorDisplay = Clean(actorDisplay);
        if (!ValidIdentity() || periodId == Uuid.Empty || actorMemberId == Uuid.Empty ||
            !Bounded(reason, 1, MaximumReasonLength) ||
            !Bounded(actorDisplay, 1, MaximumActorDisplayLength) || completedAt == default)
            return CommandFailure.InvalidContent(
                "Completing an observation-period amendment requires its exact intent and actor.");
        var index = _periods.FindIndex(item => item.PeriodId == periodId);
        if (index < 0)
            return CommandFailure.MissingRecord("The observation-period reservation was not found.");
        var period = _periods[index];
        var amendment = period.PendingAmendment;
        if (amendment is null)
            return period.LastAmendment is { } last && SameAmendment(last, periodStart,
                    periodEnd, reason, actorMemberId, actorDisplay)
                ? null
                : CommandFailure.StateConflict(
                    "The observation period has no matching reserved date amendment.");
        if (!SameAmendment(amendment, periodStart, periodEnd, reason, actorMemberId, actorDisplay) ||
            completedAt < amendment.RequestedAt || completedAt < period.UpdatedAt)
            return CommandFailure.StateConflict(
                "The completed date amendment must match its reservation and follow its request.");
        if (ConflictsWith(periodStart, periodEnd, periodId))
            return CommandFailure.StateConflict(
                "A program cannot complete an amendment into another reserved date range.");

        var ev = new ObservationPeriodAmendmentCompleted(_tenantId, _programId,
            Revision + 1, actorMemberId, periodId, amendment, completedAt);
        RaiseEvent(ev);
        return null;
    }

    public CommandFailure? CancelAmendment(Uuid periodId, DateOnly periodStart,
        DateOnly periodEnd, string reason, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset cancelledAt)
    {
        reason = Clean(reason);
        actorDisplay = Clean(actorDisplay);
        if (!ValidIdentity() || periodId == Uuid.Empty || actorMemberId == Uuid.Empty ||
            !Bounded(reason, 1, MaximumReasonLength) ||
            !Bounded(actorDisplay, 1, MaximumActorDisplayLength) || cancelledAt == default)
            return CommandFailure.InvalidContent(
                "Cancelling an observation-period amendment requires its exact intent and actor.");
        var index = _periods.FindIndex(item => item.PeriodId == periodId);
        if (index < 0)
            return CommandFailure.MissingRecord("The observation-period reservation was not found.");
        var period = _periods[index];
        var amendment = period.PendingAmendment;
        if (amendment is null)
            return null;
        if (!SameAmendment(amendment, periodStart, periodEnd, reason, actorMemberId, actorDisplay) ||
            cancelledAt < amendment.RequestedAt || cancelledAt < period.UpdatedAt)
            return CommandFailure.StateConflict(
                "The cancelled date amendment must match its reservation and follow its request.");

        var ev = new ObservationPeriodAmendmentCancelled(_tenantId, _programId,
            Revision + 1, actorMemberId, periodId, amendment, cancelledAt);
        RaiseEvent(ev);
        return null;
    }

    public CommandFailure? Release(Uuid periodId, Uuid closeSnapshotId,
        string closeSnapshotSha256, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset releasedAt)
    {
        actorDisplay = Clean(actorDisplay);
        if (!ValidIdentity() || periodId == Uuid.Empty || closeSnapshotId == Uuid.Empty ||
            !Sha256(closeSnapshotSha256) || actorMemberId == Uuid.Empty ||
            !Bounded(actorDisplay, 1, MaximumActorDisplayLength) || releasedAt == default)
            return CommandFailure.InvalidContent(
                "Releasing a Type II date range requires its exact frozen close snapshot and actor.");
        var index = _periods.FindIndex(item => item.PeriodId == periodId);
        if (index < 0)
            return CommandFailure.MissingRecord("The observation-period reservation was not found.");
        var period = _periods[index];
        if (!period.IsActive)
            return null;
        if (period.PendingAmendment is not null)
            return CommandFailure.StateConflict(
                "An observation-period amendment must finish before its date range can be released.");
        if (releasedAt < period.UpdatedAt)
            return CommandFailure.StateConflict(
                "The observation-period date range cannot be released before its latest calendar change.");

        var ev = new ObservationPeriodReservationReleased(_tenantId, _programId,
            Revision + 1, actorMemberId, periodId, closeSnapshotId,
            closeSnapshotSha256.ToLowerInvariant(), releasedAt);
        RaiseEvent(ev);
        return null;
    }

    void Apply(ObservationPeriodReserved ev)
    {
        if (ev.TenantId != _tenantId || ev.SubjectId != _programId || ev.Period is null ||
            ev.Revision != Revision + 1 || ev.ActorMemberId is not { } actor ||
            !ValidPeriod(ev.Period) || ev.Period.ReservedByMemberId != actor ||
            ev.Period.CreatedAt != ev.OccurredAt || ev.Period.UpdatedAt != ev.OccurredAt ||
            _periods.Count >= MaximumPeriods ||
            _periods.Any(item => item.PeriodId == ev.Period.PeriodId) ||
            ev.Period.IsActive &&
            ConflictsWith(ev.Period.PeriodStart, ev.Period.PeriodEnd, Uuid.Empty))
            throw new InvalidOperationException("The observation-period reservation event is invalid.");

        _periods.Add(ev.Period);
        Publish(ev.Revision, ev.OccurredAt);
    }

    void Apply(ObservationPeriodAmendmentReserved ev)
    {
        var index = _periods.FindIndex(item => item.PeriodId == ev.PeriodId);
        if (ev.TenantId != _tenantId || ev.SubjectId != _programId ||
            ev.Revision != Revision + 1 || ev.ActorMemberId == Uuid.Empty || index < 0 ||
            ev.Amendment is not { } amendment || ev.ActorMemberId != amendment.ActorMemberId ||
            amendment.RequestedAt != ev.OccurredAt || amendment.CompletedAt is not null ||
            !ValidAmendment(amendment) || !_periods[index].IsActive ||
            _periods[index].PendingAmendment is not null ||
            amendment.PeriodStart == _periods[index].PeriodStart &&
            amendment.PeriodEnd == _periods[index].PeriodEnd ||
            ConflictsWith(amendment.PeriodStart, amendment.PeriodEnd, ev.PeriodId))
            throw new InvalidOperationException("The observation-period amendment reservation event is invalid.");

        _periods[index] = _periods[index] with
        {
            PendingAmendment = amendment,
            UpdatedAt = ev.OccurredAt,
        };
        Publish(ev.Revision, ev.OccurredAt);
    }

    void Apply(ObservationPeriodAmendmentCompleted ev)
    {
        var index = _periods.FindIndex(item => item.PeriodId == ev.PeriodId);
        if (ev.TenantId != _tenantId || ev.SubjectId != _programId ||
            ev.Revision != Revision + 1 || ev.ActorMemberId == Uuid.Empty || index < 0 ||
            ev.Amendment is not { } amendment || ev.ActorMemberId != amendment.ActorMemberId ||
            !ValidAmendment(amendment) || amendment.CompletedAt is not null ||
            _periods[index].PendingAmendment != amendment ||
            ev.OccurredAt < amendment.RequestedAt || ev.OccurredAt < _periods[index].UpdatedAt ||
            ConflictsWith(amendment.PeriodStart, amendment.PeriodEnd, ev.PeriodId))
            throw new InvalidOperationException("The observation-period amendment completion event is invalid.");

        var completed = amendment with { CompletedAt = ev.OccurredAt };
        _periods[index] = _periods[index] with
        {
            PeriodStart = amendment.PeriodStart,
            PeriodEnd = amendment.PeriodEnd,
            PendingAmendment = null,
            LastAmendment = completed,
            UpdatedAt = ev.OccurredAt,
        };
        Publish(ev.Revision, ev.OccurredAt);
    }

    void Apply(ObservationPeriodAmendmentCancelled ev)
    {
        var index = _periods.FindIndex(item => item.PeriodId == ev.PeriodId);
        if (ev.TenantId != _tenantId || ev.SubjectId != _programId ||
            ev.Revision != Revision + 1 || ev.ActorMemberId == Uuid.Empty || index < 0 ||
            ev.Amendment is not { } amendment || ev.ActorMemberId != amendment.ActorMemberId ||
            !ValidAmendment(amendment) || amendment.CompletedAt is not null ||
            _periods[index].PendingAmendment != amendment ||
            ev.OccurredAt < amendment.RequestedAt || ev.OccurredAt < _periods[index].UpdatedAt)
            throw new InvalidOperationException("The observation-period amendment cancellation event is invalid.");

        _periods[index] = _periods[index] with
        {
            PendingAmendment = null,
            UpdatedAt = ev.OccurredAt,
        };
        Publish(ev.Revision, ev.OccurredAt);
    }

    void Apply(ObservationPeriodReservationReleased ev)
    {
        var index = _periods.FindIndex(item => item.PeriodId == ev.PeriodId);
        if (ev.TenantId != _tenantId || ev.SubjectId != _programId ||
            ev.Revision != Revision + 1 || ev.ActorMemberId == Uuid.Empty ||
            ev.CloseSnapshotId == Uuid.Empty || !Sha256(ev.CloseSnapshotSha256) || index < 0 ||
            !_periods[index].IsActive || _periods[index].PendingAmendment is not null ||
            ev.OccurredAt < _periods[index].UpdatedAt)
            throw new InvalidOperationException("The observation-period reservation release event is invalid.");

        _periods[index] = _periods[index] with
        {
            IsActive = false,
            UpdatedAt = ev.OccurredAt,
        };
        Publish(ev.Revision, ev.OccurredAt);
    }

    void Publish(long revision, DateTimeOffset at)
    {
        _periods.Sort(static (left, right) =>
        {
            var start = left.PeriodStart.CompareTo(right.PeriodStart);
            return start != 0 ? start : StringComparer.Ordinal.Compare(
                left.PeriodId.ToString(), right.PeriodId.ToString());
        });
        _current = new TypeIIObservationCalendarView(_tenantId, _programId, revision,
            Array.AsReadOnly(_periods.ToArray()), at);
    }

    bool ValidIdentity() => _tenantId != Uuid.Empty && _programId != Uuid.Empty && Id == _programId;

    static bool ValidPeriod(TypeIIObservationCalendarPeriodView period) =>
        period.PeriodId != Uuid.Empty && period.ExaminationId != Uuid.Empty &&
        period.PlanId != Uuid.Empty && ValidRange(period.PeriodStart, period.PeriodEnd) &&
        Sha256(period.OpeningSnapshotSha256) && period.ReservedByMemberId != Uuid.Empty &&
        Bounded(period.ReservedByDisplay, 1, MaximumActorDisplayLength) &&
        period.CreatedAt != default && period.UpdatedAt >= period.CreatedAt &&
        (period.PendingAmendment is null || ValidAmendment(period.PendingAmendment) &&
            period.PendingAmendment.CompletedAt is null) &&
        (period.LastAmendment is null || ValidAmendment(period.LastAmendment) &&
            period.LastAmendment.CompletedAt is not null);

    bool ConflictsWith(DateOnly periodStart, DateOnly periodEnd, Uuid exceptPeriodId) =>
        _periods.Any(item => item.IsActive && item.PeriodId != exceptPeriodId &&
            (Overlaps(periodStart, periodEnd, item.PeriodStart, item.PeriodEnd) ||
             item.PendingAmendment is { } pending &&
             Overlaps(periodStart, periodEnd, pending.PeriodStart, pending.PeriodEnd)));

    static bool ValidAmendment(TypeIIObservationCalendarAmendmentView amendment) =>
        ValidRange(amendment.PeriodStart, amendment.PeriodEnd) &&
        Bounded(amendment.Reason, 1, MaximumReasonLength) &&
        amendment.ActorMemberId != Uuid.Empty &&
        Bounded(amendment.ActorDisplay, 1, MaximumActorDisplayLength) &&
        amendment.RequestedAt != default &&
        (amendment.CompletedAt is null || amendment.CompletedAt >= amendment.RequestedAt);

    static bool SameAmendment(TypeIIObservationCalendarAmendmentView existing,
        DateOnly periodStart, DateOnly periodEnd, string reason, Uuid actorMemberId,
        string actorDisplay) => existing.PeriodStart == periodStart &&
        existing.PeriodEnd == periodEnd && existing.Reason == reason &&
        existing.ActorMemberId == actorMemberId && existing.ActorDisplay == actorDisplay;

    static bool ValidRange(DateOnly start, DateOnly end) => start != default && end >= start;

    static bool Overlaps(DateOnly leftStart, DateOnly leftEnd, DateOnly rightStart,
        DateOnly rightEnd) => leftStart <= rightEnd && leftEnd >= rightStart;

    static bool Sha256(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);

    static bool HashEquals(string? left, string? right) => left is not null && right is not null &&
        StringComparer.OrdinalIgnoreCase.Equals(left, right);

    static string Clean(string? value) => value?.Trim() ?? string.Empty;

    static bool Bounded(string? value, int minimum, int maximum) => value is not null &&
        value.Trim().Length >= minimum && value.Trim().Length <= maximum;
}
