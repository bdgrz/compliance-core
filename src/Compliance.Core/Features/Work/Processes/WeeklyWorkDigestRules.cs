using System.Collections.ObjectModel;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
namespace Bdgrz.Compliance.Features.Work.Processes;

/// <summary>
///     Owns one member's weekly email intent and its delivery attempts. Queue state remains in its
///     source workflows; this stream records only scheduling, suppression, and transport outcomes.
/// </summary>
public sealed class WeeklyWorkDigestRules
{
    public const string SchedulerProcessId = "scheduler:WeeklyWorkDigestV1";
    public const string DeliveryProcessId = "reactor:WeeklyWorkDigestDeliveryV1";
    const int MaximumScheduleKeyLength = 200;
    const int MaximumDeliveryKeyLength = 200;
    const int MaximumFailureCodeLength = 200;

    readonly Uuid _tenantId;
    readonly List<WorkDigestDeliveryAttemptView> _attempts = [];
    Uuid _memberId;
    DateOnly _weekStarting;
    string? _scheduleKey;
    DateTimeOffset? _scheduledAt;
    ActorReference? _scheduledBy;
    string _status = "missing";
    string? _suppressionReason;

    public long Revision { get; private set; }
    public string Status => _status;
    public Uuid MemberId => _memberId;
    public DateOnly WeekStarting => _weekStarting;
    public IReadOnlyList<WorkDigestDeliveryAttemptView> DeliveryAttempts => ReadOnly(_attempts);

    readonly List<DomainEvent> _pending = [];
    int _attemptCount;
    public Uuid Id { get; }
    public IReadOnlyList<DomainEvent> PendingEvents => _pending;

    public WeeklyWorkDigestRules(Uuid tenantId, Uuid digestId)
    {
        (_tenantId, Id) = (tenantId, digestId);
    }

    internal void Replay(DomainEvent fact)
    {
        switch (fact)
        {
            case WeeklyWorkDigestScheduled scheduled:
                Apply(scheduled);
                break;
            case WeeklyWorkDigestDeliveryRequested requested:
                Apply(requested);
                break;
            case WeeklyWorkDigestDeliveryRecorded recorded:
                Apply(recorded);
                break;
            case WeeklyWorkDigestDeliverySuppressed suppressed:
                Apply(suppressed);
                break;
            default:
                throw new InvalidOperationException("The weekly rules source kind is unsupported.");
        }
    }
    void RaiseEvent(DomainEvent fact)
    {
        Replay(fact);
        _pending.Add(fact);
    }

    internal void Restore(WeeklyWorkDigestHeader header, WorkDigestDeliveryAttemptView? selected,
        WorkDigestDeliveryAttemptView? latest)
    {
        (_memberId, _weekStarting, _scheduleKey, _scheduledAt, _scheduledBy, _status, Revision, _suppressionReason) =
            (header.MemberId, header.WeekStarting, header.ScheduleKey, header.ScheduledAt, header.ScheduledBy,
                header.Status, header.Revision, header.SuppressionReason);
        _attemptCount = latest?.Number ?? 0;
        if (selected is not null && selected.Number != latest?.Number)
            _attempts.Add(selected);
        if (latest is not null)
            _attempts.Add(latest);
    }

    /// <summary>Only a fully verified native NoChange receipt advances this resource counter without a business effect.</summary>
    internal void AdvanceReceiptRevision(long revision)
    {
        if (revision != Revision + 1)
            throw new InvalidOperationException("A native receipt must advance exactly one owning revision.");
        Revision = revision;
    }

    public static Uuid IdFor(Uuid tenantId, Uuid memberId, DateOnly weekStarting)
    {
        if (tenantId == Uuid.Empty || memberId == Uuid.Empty || !IsMonday(weekStarting))
            throw new ArgumentException("A weekly digest identity requires a tenant, member, and Monday week start.");
        return Uuid.CreateVersion5(tenantId,
            $"weekly-work-digest-v1:{memberId}:{weekStarting:yyyy-MM-dd}");
    }

    public static string DeliveryKeyFor(Uuid digestId, int attemptNumber)
    {
        if (digestId == Uuid.Empty || attemptNumber <= 0)
            throw new ArgumentException("A delivery key requires a digest and positive attempt number.");
        return Uuid.CreateVersion5(digestId, $"work-digest-delivery-v1:{attemptNumber}").ToString();
    }

    public WeeklyWorkDigestView Read() => new(_tenantId, Id, _memberId, _weekStarting, _status,
        Revision, _scheduleKey, _scheduledAt, _scheduledBy, ReadOnly(_attempts), _suppressionReason);

    public Result Schedule(Uuid memberId, DateOnly weekStarting, string scheduleKey,
        ActorReference scheduledBy, DateTimeOffset scheduledAt)
    {
        if (_tenantId == Uuid.Empty || Id == Uuid.Empty || memberId == Uuid.Empty ||
            !IsMonday(weekStarting) || IdFor(_tenantId, memberId, weekStarting) != Id ||
            !Bounded(scheduleKey, 1, MaximumScheduleKeyLength) ||
            !ValidActor(scheduledBy, SchedulerProcessId) || scheduledAt == default)
            return Invalid("Scheduling requires the stable member-week identity, scheduler, key, and time.");
        if (_status != "missing")
            return _memberId == memberId && _weekStarting == weekStarting &&
                   _scheduleKey == scheduleKey
                ? Result.Success
                : Conflict("The weekly digest identity already belongs to different schedule intent.");

        RaiseEvent(new WeeklyWorkDigestScheduled(_tenantId, Id, memberId, weekStarting,
            scheduleKey.Trim(), scheduledBy, scheduledAt));
        return Result.Success;
    }

    public Result RequestDelivery(long expectedRevision, int attemptNumber, string deliveryKey,
        ActorReference requestedBy, DateTimeOffset requestedAt)
    {
        var existing = _attempts.FirstOrDefault(attempt => attempt.DeliveryKey == deliveryKey);
        if (existing is not null)
            return existing.Number == attemptNumber && existing.RequestedBy == requestedBy
                ? Result.Success
                : Conflict("The weekly digest delivery key already has different intent.");
        if (_status is not ("scheduled" or "delivery_failed"))
            return Conflict("Only a scheduled digest or failed delivery can be sent.");
        if (expectedRevision != Revision)
            return Conflict($"The weekly digest revision is {Revision}.");
        if (attemptNumber != _attemptCount + 1 ||
            deliveryKey != DeliveryKeyFor(Id, attemptNumber) ||
            !ValidActor(requestedBy, DeliveryProcessId) || requestedAt == default ||
            (_attempts.Count > 0 && _attempts[^1].CompletedAt is { } completedAt &&
             requestedAt < completedAt))
            return Invalid("A delivery request requires the next deterministic attempt and named worker.");

        RaiseEvent(new WeeklyWorkDigestDeliveryRequested(_tenantId, Id, _memberId,
            Revision + 1, attemptNumber, deliveryKey, requestedBy, requestedAt));
        return Result.Success;
    }

    public Result SuppressDelivery(long expectedRevision, string reason, ActorReference suppressedBy,
        DateTimeOffset suppressedAt)
    {
        if (_status == "suppressed" && _suppressionReason == reason && suppressedBy.Kind == "system_process")
            return Result.Success;
        if (_status is not ("scheduled" or "delivery_failed") ||
            (_attempts.Count > 0 && _attempts[^1].Status != "failed"))
            return Conflict("Only a scheduled digest without delivery attempts can be suppressed.");
        if (expectedRevision != Revision)
            return Conflict($"The weekly digest revision is {Revision}.");
        if (reason is not ("member_inactive" or "email_digest_opted_out" or
                           "verified_email_unavailable" or "guest_member") ||
            !ValidActor(suppressedBy, DeliveryProcessId) || suppressedAt == default)
            return Invalid("Digest suppression requires a supported reason and named worker.");

        RaiseEvent(new WeeklyWorkDigestDeliverySuppressed(_tenantId, Id, _memberId,
            Revision + 1, reason, suppressedBy, suppressedAt));
        return Result.Success;
    }

    public Result RecordDeliveryResult(long expectedRevision, int attemptNumber, string deliveryKey,
        string outcome, string? failureCode, ActorReference recordedBy, DateTimeOffset attemptedAt)
    {
        var matching = _attempts.FirstOrDefault(attempt => attempt.Number == attemptNumber &&
            attempt.DeliveryKey == deliveryKey);
        if (matching is not null && matching.Status != "requested")
            return matching.Status == outcome && matching.FailureCode == failureCode &&
                   matching.CompletedAt == attemptedAt && matching.RecordedBy == recordedBy
                ? Result.Success
                : Conflict("The weekly digest delivery result differs from the recorded callback.");
        if (matching is null || _attempts[^1].Number != matching.Number ||
            matching.Status != "requested")
            return Conflict("The delivery result does not match the current weekly digest attempt.");
        if (expectedRevision != Revision)
            return Conflict($"The weekly digest revision is {Revision}.");
        if (outcome is not ("delivered" or "failed" or "skipped_empty") ||
            (outcome == "failed"
                ? !Bounded(failureCode, 1, MaximumFailureCodeLength)
                : failureCode is not null) ||
            !ValidActor(recordedBy, DeliveryProcessId) || attemptedAt < matching.RequestedAt)
            return Invalid("A delivery result requires a supported outcome, matching failure code, and named worker.");

        RaiseEvent(new WeeklyWorkDigestDeliveryRecorded(_tenantId, Id, _memberId,
            Revision + 1, attemptNumber, deliveryKey, outcome, failureCode?.Trim(), recordedBy,
            attemptedAt));
        return Result.Success;
    }

    void Apply(WeeklyWorkDigestScheduled ev)
    {
        if (Revision != 0 || ev.TenantId != _tenantId || ev.DigestId != Id ||
            ev.MemberId == Uuid.Empty || !IsMonday(ev.WeekStarting) ||
            IdFor(ev.TenantId, ev.MemberId, ev.WeekStarting) != Id ||
            !Bounded(ev.ScheduleKey, 1, MaximumScheduleKeyLength) ||
            !ValidActor(ev.ScheduledBy, SchedulerProcessId) || ev.ScheduledAt == default)
            throw new InvalidOperationException("The weekly work digest schedule event is invalid.");
        _memberId = ev.MemberId;
        _weekStarting = ev.WeekStarting;
        _scheduleKey = ev.ScheduleKey;
        _scheduledAt = ev.ScheduledAt;
        _scheduledBy = ev.ScheduledBy;
        _status = "scheduled";
        Revision = 1;
    }

    void Apply(WeeklyWorkDigestDeliveryRequested ev)
    {
        if (ev.TenantId != _tenantId || ev.DigestId != Id || ev.MemberId != _memberId ||
            ev.Revision != Revision + 1 || ev.AttemptNumber != _attemptCount + 1 ||
            ev.DeliveryKey != DeliveryKeyFor(Id, ev.AttemptNumber) ||
            _status is not ("scheduled" or "delivery_failed") ||
            !ValidActor(ev.RequestedBy, DeliveryProcessId) || ev.RequestedAt == default ||
            (_attempts.Count > 0 && _attempts[^1].CompletedAt is { } completedAt &&
             ev.RequestedAt < completedAt))
            throw new InvalidOperationException("The weekly digest delivery request event is invalid.");
        _attemptCount = ev.AttemptNumber;
        _attempts.Add(new WorkDigestDeliveryAttemptView(ev.AttemptNumber, ev.DeliveryKey,
            "requested", ev.RequestedAt, ev.RequestedBy, null, null, null));
        _status = "delivery_requested";
        Revision = ev.Revision;
    }

    void Apply(WeeklyWorkDigestDeliveryRecorded ev)
    {
        if (ev.TenantId != _tenantId || ev.DigestId != Id || ev.MemberId != _memberId ||
            ev.Revision != Revision + 1 || _attempts.Count == 0 ||
            _attempts[^1] is not { Status: "requested" } current ||
            current.Number != ev.AttemptNumber || current.DeliveryKey != ev.DeliveryKey ||
            ev.Outcome is not ("delivered" or "failed" or "skipped_empty") ||
            (ev.Outcome == "failed"
                ? !Bounded(ev.FailureCode, 1, MaximumFailureCodeLength)
                : ev.FailureCode is not null) ||
            !ValidActor(ev.RecordedBy, DeliveryProcessId) || ev.AttemptedAt < current.RequestedAt)
            throw new InvalidOperationException("The weekly digest delivery result event is invalid.");
        _attempts[^1] = current with
        {
            Status = ev.Outcome,
            CompletedAt = ev.AttemptedAt,
            RecordedBy = ev.RecordedBy,
            FailureCode = ev.FailureCode,
        };
        _status = StatusFor(ev.Outcome);
        Revision = ev.Revision;
    }

    void Apply(WeeklyWorkDigestDeliverySuppressed ev)
    {
        if (ev.TenantId != _tenantId || ev.DigestId != Id || ev.MemberId != _memberId ||
            ev.Revision != Revision + 1 ||
            _status is not ("scheduled" or "delivery_failed") ||
            (_attempts.Count > 0 && _attempts[^1].Status != "failed") ||
            ev.Reason is not ("member_inactive" or "email_digest_opted_out" or
                              "verified_email_unavailable" or "guest_member") ||
            !ValidActor(ev.SuppressedBy, DeliveryProcessId) || ev.SuppressedAt == default)
            throw new InvalidOperationException("The weekly digest suppression event is invalid.");
        _suppressionReason = ev.Reason;
        _status = "suppressed";
        Revision = ev.Revision;
    }

    static string StatusFor(string outcome) => outcome switch
    {
        "delivered" => "delivered",
        "failed" => "delivery_failed",
        "skipped_empty" => "empty",
        _ => "invalid",
    };

    static bool IsMonday(DateOnly date) => date != default && date.DayOfWeek == DayOfWeek.Monday;

    static bool Bounded(string? value, int minimum, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value == value.Trim() && value.Length >= minimum &&
        value.Length <= maximum && !value.Any(char.IsControl);

    static bool ValidActor(ActorReference? actor, string processId) =>
        actor is { Kind: "system_process", Id: var id, Display: var display } && id == processId &&
        Bounded(display, 1, 2000);

    static ReadOnlyCollection<WorkDigestDeliveryAttemptView> ReadOnly(
        IReadOnlyCollection<WorkDigestDeliveryAttemptView> source) => Array.AsReadOnly(source.ToArray());

    static Result Invalid(string message) => Result.Failure(new RequestError(RequestErrorKind.Validation, message));
    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
