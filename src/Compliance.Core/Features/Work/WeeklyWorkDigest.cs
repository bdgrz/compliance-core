using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Bounded current and explicit selected attempt authority; full attempts remain external immutable history.</summary>
public sealed class WeeklyWorkDigest : Aggregate
{
    public const string SchedulerProcessId = WeeklyWorkDigestRules.SchedulerProcessId;
    public const string DeliveryProcessId = WeeklyWorkDigestRules.DeliveryProcessId;
    public const string Area = "weekly-work-digests";
    readonly Uuid _tenant;
    readonly int _selectedAttempt;
    WorkDigestDeliveryAttemptView? _latest;
    WorkDigestDeliveryAttemptView? _selected;
    public WeeklyWorkDigest(Uuid tenantId, Uuid digestId, int selectedAttempt = 0)
        : base(digestId, new EventStreamAddress(tenantId.ToString(), Area, digestId.ToString()))
    {
        (_tenant, _selectedAttempt) = (tenantId, selectedAttempt);
        On<WeeklyWorkDigestScheduled>(ApplyFact);
        On<WeeklyWorkDigestDeliveryRequested>(ApplyFact);
        On<WeeklyWorkDigestDeliveryRecorded>(ApplyFact);
        On<WeeklyWorkDigestDeliverySuppressed>(ApplyFact);
    }
    public Uuid TenantId => _tenant;
    public WeeklyWorkDigestHeader? Header { get; private set; }
    public long Revision => Header?.Revision ?? 0;
    public string Status => Header?.Status ?? "missing";
    public Uuid MemberId => Header?.MemberId ?? Uuid.Empty;
    public DateOnly WeekStarting => Header?.WeekStarting ?? default;
    public WorkDigestDeliveryAttemptView? LatestAttempt => _latest;
    public WorkDigestDeliveryAttemptView? SelectedAttempt => _selectedAttempt == _latest?.Number ? _latest : _selected;
    public int SelectedAttemptNumber => _selectedAttempt;
    public DomainEvent? Latest { get; private set; }
    internal WorkDigestReplayCut ReplayCut { get; } = new();
    public static Uuid IdFor(Uuid tenantId, Uuid memberId, DateOnly weekStarting) =>
        WeeklyWorkDigestRules.IdFor(tenantId, memberId, weekStarting);
    public static string DeliveryKeyFor(Uuid digestId, int attemptNumber) =>
        WeeklyWorkDigestRules.DeliveryKeyFor(digestId, attemptNumber);

    WeeklyWorkDigestRules Rules()
    {
        var rules = new WeeklyWorkDigestRules(_tenant, Id);
        if (Header is { } header)
            rules.Restore(header, _selected, _latest);
        return rules;
    }
    Result Change(Func<WeeklyWorkDigestRules, Result> operation)
    {
        if (!ReplayCut.Allows(this))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact weekly digest source cut is not finalized; retry.", true));
        var rules = Rules();
        var result = operation(rules);
        if (result.IsSuccess && rules.PendingEvents.Count > 0)
            RaiseEvent(rules.PendingEvents.Single());
        return result;
    }
    public Result Schedule(Uuid memberId, DateOnly weekStarting, string key, ActorReference by, DateTimeOffset at) =>
        Change(rules => rules.Schedule(memberId, weekStarting, key, by, at));
    public Result RequestDelivery(long expected, int attempt, string key, ActorReference by, DateTimeOffset at) =>
        Change(rules => rules.RequestDelivery(expected, attempt, key, by, at));
    public Result SuppressDelivery(long expected, string reason, ActorReference by, DateTimeOffset at) =>
        Change(rules => rules.SuppressDelivery(expected, reason, by, at));
    public Result RecordDeliveryResult(long expected, int attempt, string key, string outcome, string? code,
        ActorReference by, DateTimeOffset at) => Change(rules => rules.RecordDeliveryResult(expected, attempt, key, outcome, code, by, at));
    void ApplyFact(DomainEvent fact)
    {
        var rules = Rules();
        try
        { rules.Replay(fact); }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            throw new ImmutableContentIntegrityException("The weekly digest native transition is invalid: " + exception.Message);
        }
        var view = rules.Read();
        Header = new WeeklyWorkDigestHeader(view.MemberId, view.WeekStarting, view.ScheduleKey, view.ScheduledAt,
            view.ScheduledBy, view.Status, view.Revision, view.SuppressionReason);
        _latest = view.DeliveryAttempts.Count == 0 ? null : view.DeliveryAttempts[^1];
        if (_selectedAttempt > 0 && view.DeliveryAttempts.FirstOrDefault(item => item.Number == _selectedAttempt) is { } selected)
            _selected = selected;
        Latest = fact;
        ReplayCut.Observe(fact);
    }
}
