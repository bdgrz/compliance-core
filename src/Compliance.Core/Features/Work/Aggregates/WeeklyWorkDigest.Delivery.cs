using Bdgrz.Compliance.Features.Work.Events;
using Bdgrz.Compliance.Features.Work.History;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Rules;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Aggregates;

public sealed partial class WeeklyWorkDigest
{
    readonly Uuid _deliveryMember;
    readonly DateOnly _deliveryWeek;
    WorkDigestDispatchState? _deliveryState;
    public bool DeliveryMode => _deliveryMember != Uuid.Empty;
    public WeeklyWorkDigestSent? SentResult { get; private set; }
    public WeeklyWorkDigestSkipped? SkippedResult { get; private set; }

    public static WeeklyWorkDigest ForDelivery(Uuid tenantId, Uuid memberId, DateOnly weekOf)
    {
        if (tenantId == Uuid.Empty || memberId == Uuid.Empty || weekOf == default || weekOf.DayOfWeek != DayOfWeek.Monday)
            throw new ArgumentException("A weekly delivery needs its tenant, member and Monday identity.");
        return new WeeklyWorkDigest(tenantId, IdFor(tenantId, memberId, weekOf), deliveryMember: memberId, deliveryWeek: weekOf);
    }

    internal WorkDigestDispatchStatusView ReadDelivery() => (_deliveryState?.View(TenantId, _deliveryMember) ??
        new WorkDigestDispatchStatusView(TenantId, _deliveryMember, _deliveryWeek, "UTC", DateTimeOffset.MinValue,
            "not_scheduled", 0, null, Uuid.Empty, DateTimeOffset.MinValue, null, null, null, null, null)) with
        { Revision = checked((long)Version) };

    internal bool ScheduleDelivery(WorkDigestScheduleWindow window, TimeSpan retryWindow, DateTimeOffset at)
    {
        if (!DeliveryMode || _deliveryState is not null || window.WeekOf != _deliveryWeek || at < window.ScheduledAt ||
            !WorkDigestSchedule.IsCurrentWeek(window.WeekOf, window.TimeZoneId, at))
            return false;
        var rules = new WorkDigestDispatchRules(TenantId, _deliveryMember);
        if (!rules.Schedule(window, Uuid.CreateVersion5(Id, "message"), retryWindow, at))
            return false;
        RaiseEvent(rules.PendingEvents.Single());
        return true;
    }

    internal bool StartDeliveryAttempt(DateTimeOffset at) => ChangeDelivery(rules => rules.StartAttempt(_deliveryWeek, at));
    internal bool RecordDeliveryUnknown(DateTimeOffset at, string reason) => ChangeDelivery(rules => rules.RecordUnknown(_deliveryWeek, at, reason));
    internal bool RecoverInterruptedDelivery(DateTimeOffset at) => ChangeDelivery(rules => rules.RecordInterruptedAttempt(_deliveryWeek, at));
    internal bool RetryUnknownDelivery(DateTimeOffset at, int maximumAttempts, TimeSpan delay) =>
        ChangeDelivery(rules => rules.RetryUnknownAutomatically(_deliveryWeek, at, maximumAttempts, delay));
    internal bool RetryRejectedDelivery(DateTimeOffset at, int maximumAttempts, TimeSpan delay) =>
        ChangeDelivery(rules => rules.RecordTransientRejection(_deliveryWeek, at, maximumAttempts, delay));
    internal bool RecordRejectedDelivery(DateTimeOffset at, string reason) =>
        ChangeDelivery(rules => rules.RecordPermanentRejection(_deliveryWeek, at, reason));
    internal bool ExpireDelivery(DateTimeOffset at) => ChangeDelivery(rules => rules.RecordRetryWindowExpired(_deliveryWeek, at));

    bool ChangeDelivery(Func<WorkDigestDispatchRules, bool> change)
    {
        if (!DeliveryMode || _deliveryState is null || SentResult is not null || SkippedResult is not null)
            return false;
        var rules = new WorkDigestDispatchRules(TenantId, _deliveryMember);
        rules.Restore(_deliveryState);
        if (!change(rules))
            return false;
        RaiseEvent(rules.PendingEvents.Single());
        return true;
    }

    internal bool RecordWeeklySent(WorkDigestDeliveryMessage message, DateTimeOffset at)
    {
        if (_deliveryState is not { Status: WorkDigestDispatch.InFlight } state || SentResult is not null || SkippedResult is not null ||
            message.MessageId != state.MessageId || message.ItemKeys is not { Count: > 0 and <= 200 } keys ||
            keys.Any(key => key == Uuid.Empty) || keys.Distinct().Count() != keys.Count ||
            string.IsNullOrWhiteSpace(message.Recipient) || message.Recipient.Length > 320 || message.Recipient.Any(char.IsControl) ||
            at < state.LastUpdatedAt)
            return false;
        RaiseEvent(new WeeklyWorkDigestSent(TenantId, _deliveryMember, _deliveryWeek, state.TimeZoneId,
            state.MessageId, keys.ToArray(), message.Recipient, at));
        return true;
    }

    internal bool RecordWeeklySkipped(string reason, DateTimeOffset at)
    {
        if (_deliveryState is not { } state || SentResult is not null || SkippedResult is not null ||
            state.Status is not (WorkDigestDispatch.Scheduled or WorkDigestDispatch.RetryPending or WorkDigestDispatch.Unknown or WorkDigestDispatch.InFlight) ||
            reason is not ("empty" or "opted_out" or "no_verified_email" or "inactive_member") || at < state.LastUpdatedAt)
            return false;
        RaiseEvent(new WeeklyWorkDigestSkipped(TenantId, _deliveryMember, _deliveryWeek, state.TimeZoneId, reason, at));
        return true;
    }

    void ApplyDeliveryTechnical(DomainEvent fact)
    {
        var (tenant, member, week) = WorkDigestDispatchHistory.Scope(fact);
        if (!DeliveryMode || tenant != TenantId || member != _deliveryMember || week != _deliveryWeek ||
            SentResult is not null || SkippedResult is not null)
            throw new InvalidOperationException("A weekly delivery fact differs from its exact member-week owner or terminal result.");
        var rules = new WorkDigestDispatchRules(TenantId, _deliveryMember);
        if (_deliveryState is { } state)
            rules.Restore(state);
        var accepted = fact switch
        {
            WorkDigestDispatchScheduled scheduled => _deliveryState is null && scheduled.MessageId == Uuid.CreateVersion5(Id, "message") &&
                scheduled.TimeZoneId == WorkDigestSchedule.NormalizeTimeZoneId(scheduled.TimeZoneId) &&
                scheduled.RecordedAt >= scheduled.ScheduledAt && scheduled.RetryDeadline > scheduled.ScheduledAt &&
                scheduled.RetryDeadline <= WorkDigestSchedule.NextWeekStartUtc(week, scheduled.TimeZoneId) &&
                rules.Schedule(new WorkDigestScheduleWindow(week, scheduled.TimeZoneId, scheduled.ScheduledAt),
                    scheduled.MessageId, scheduled.RetryDeadline - scheduled.ScheduledAt, scheduled.RecordedAt),
            WorkDigestDispatchAttemptStarted started => started.Attempt == (_deliveryState?.Attempts ?? 0) + 1 && rules.StartAttempt(week, started.StartedAt),
            WorkDigestDispatchOutcomeRecorded outcome => ValidDeliveryOutcome(outcome),
            _ => false
        };
        if (!accepted)
            throw new InvalidOperationException("The weekly delivery technical transition has no matching current schedule or attempt.");
        if (fact is WorkDigestDispatchOutcomeRecorded recorded)
        {
            rules = new WorkDigestDispatchRules(TenantId, _deliveryMember);
            rules.Restore(_deliveryState!);
            rules.Replay(recorded);
        }
        _deliveryState = rules.States[week];
    }

    bool ValidDeliveryOutcome(WorkDigestDispatchOutcomeRecorded fact)
    {
        if (_deliveryState is not { } state || fact.RecordedAt < state.LastUpdatedAt ||
            (fact.Status == WorkDigestDispatch.RetryPending) != (fact.NextAttemptAt is not null))
            return false;
        return fact.Status switch
        {
            WorkDigestDispatch.Unknown or WorkDigestDispatch.PermanentFailure => state.Status == WorkDigestDispatch.InFlight,
            WorkDigestDispatch.RetryPending => state.Status is WorkDigestDispatch.InFlight or WorkDigestDispatch.Unknown &&
                fact.NextAttemptAt >= fact.RecordedAt && fact.NextAttemptAt < state.RetryDeadline,
            WorkDigestDispatch.RetryExhausted => state.Status is WorkDigestDispatch.Scheduled or WorkDigestDispatch.RetryPending or WorkDigestDispatch.Unknown or WorkDigestDispatch.InFlight,
            _ => false
        };
    }

    void ApplyWeeklySent(WeeklyWorkDigestSent fact)
    {
        var message = new WorkDigestDeliveryMessage(fact.MessageId, fact.Destination, "", "", fact.ItemKeys);
        if (!DeliveryScope(fact.TenantId, fact.MemberId, fact.WeekOf, fact.TimeZoneId) ||
            _deliveryState is not { Status: WorkDigestDispatch.InFlight } state ||
            message.MessageId != state.MessageId || fact.ItemKeys is not { Count: > 0 and <= 200 } ||
            fact.ItemKeys.Any(key => key == Uuid.Empty) || fact.ItemKeys.Distinct().Count() != fact.ItemKeys.Count ||
            string.IsNullOrWhiteSpace(fact.Destination) || fact.Destination.Length > 320 || fact.Destination.Any(char.IsControl) ||
            fact.SentAt < state.LastUpdatedAt)
            throw new InvalidOperationException("The sent digest differs from its exact frozen message and member-week authority.");
        SentResult = fact;
        _deliveryState = state with { Status = WorkDigestDispatch.Sent, LastUpdatedAt = fact.SentAt, FailureCode = null, NextAttemptAt = null };
    }

    void ApplyWeeklySkipped(WeeklyWorkDigestSkipped fact)
    {
        if (!DeliveryScope(fact.TenantId, fact.MemberId, fact.WeekOf, fact.TimeZoneId) || _deliveryState is not { } state ||
            state.Status is not (WorkDigestDispatch.Scheduled or WorkDigestDispatch.RetryPending or WorkDigestDispatch.Unknown or WorkDigestDispatch.InFlight) ||
            fact.Reason is not ("empty" or "opted_out" or "no_verified_email" or "inactive_member") || fact.SkippedAt < state.LastUpdatedAt)
            throw new InvalidOperationException("The skipped digest differs from its current frozen member-week authority.");
        SkippedResult = fact;
        _deliveryState = state with { Status = WorkDigestDispatch.Skipped, LastUpdatedAt = fact.SkippedAt, FailureCode = fact.Reason, NextAttemptAt = null };
    }

    bool DeliveryScope(Uuid tenant, Uuid member, DateOnly week, string zone) => DeliveryMode &&
        tenant == TenantId && member == _deliveryMember && week == _deliveryWeek && _deliveryState?.TimeZoneId == zone &&
        SentResult is null && SkippedResult is null;
}
