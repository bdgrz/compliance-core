using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.History;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
namespace Bdgrz.Compliance.Features.Work.Aggregates;

/// <summary>
///     One member's weekly email digest preference. Members are subscribed until they opt out; the
///     preference never affects in-app reminders.
/// </summary>
public sealed class WorkDigestPreference : Aggregate
{
    readonly Uuid _tenantId;
    bool _emailDigestEnabled = true;
    DateTimeOffset? _changedAt;
    string _timeZoneId = "UTC";
    bool _emitting;
    internal NativeAcceptance Acceptance { get; }
    internal WorkPreferenceReplayCut ReplayCut { get; } = new();
    internal Uuid TenantId => _tenantId;

    public WorkDigestPreference(Uuid tenantId, Uuid memberId, Uuid commandId = default)
        : base(memberId, new EventStreamAddress(tenantId.ToString(), "work-digest-preferences",
            memberId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(commandId);
        On<WorkDigestPreferenceRecorded>(fact =>
        {
            if (fact.Operation is null || fact.Preference is null || !WorkDigestSchedule.IsValidTimeZone(fact.AuthoredTimeZoneId))
                throw new ImmutableContentIntegrityException("The preference receipt has invalid authored values.");
            var expected = Decide(Read(), fact.EmailDigestEnabled, fact.AuthoredTimeZoneId, fact.Operation.AcceptedAt);
            if (fact.TenantId != _tenantId || fact.MemberId != Id || fact.Preference != expected ||
                fact.NoChange != (expected == Read()))
                throw new ImmutableContentIntegrityException("The preference receipt differs from its current scalar authority.");
            Acceptance.Observe(fact);
            (_emailDigestEnabled, _changedAt, _timeZoneId) = (expected.EmailDigestEnabled, expected.ChangedAt, expected.TimeZoneId);
        });
        On<WorkDigestPreferenceChanged>(ev =>
        {
            _emailDigestEnabled = ev.EmailDigestEnabled;
            _changedAt = ev.ChangedAt;
            _timeZoneId = WorkDigestSchedule.NormalizeTimeZoneId(ev.TimeZoneId);
        });
    }

    public WorkDigestPreferenceView Read() => new(_emailDigestEnabled, _changedAt)
    {
        TimeZoneId = _timeZoneId,
    };

    internal static WorkDigestPreferenceView Decide(WorkDigestPreferenceView before, bool enabled, string? zone, DateTimeOffset at)
    {
        var normalized = zone is null ? before.TimeZoneId : WorkDigestSchedule.NormalizeTimeZoneId(zone);
        return before.EmailDigestEnabled == enabled && before.TimeZoneId == normalized ? before
            : new WorkDigestPreferenceView(enabled, at) { TimeZoneId = normalized };
    }

    internal Result Record(WorkDigestPreferenceRecorded fact, ulong preparedVersion, ulong preparedPosition)
    {
        var verified = ReplayCut.Require(this);
        if (!verified.IsSuccess)
            return verified;
        if (Version != preparedVersion || CommittedStreamPosition != preparedPosition ||
            Acceptance.SelectedLogicalId != fact.Operation.LogicalId || Acceptance.SelectedFact is not null ||
            !NativeOwnerHistory.IsOwnRoot(this, fact.Operation, nameof(SetWorkDigestPreference), null) ||
            fact.FirstResponse is null || fact.OperationResult is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "Reload the exact current preference cut.", true));
        if (DomainEventPayload.Size(fact) > DomainEventPayload.MaximumBytes)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The complete preference receipt exceeds the native fact payload budget."));
        _emitting = true;
        try
        { RaiseEvent(fact); }
        finally { _emitting = false; }
        return Result.Success;
    }

    protected override void Apply(DomainEvent ev)
    {
        base.Apply(ev);
        ReplayCut.Observe(ev, _emitting);
    }

    /// <summary>Records a change; setting the current value again records nothing.</summary>
    public void Set(bool emailDigestEnabled, ActorReference actor, DateTimeOffset at)
        => Set(emailDigestEnabled, null, actor, at);

    /// <summary>Records the email preference and optional member-local scheduling zone.</summary>
    public void Set(bool emailDigestEnabled, string? timeZoneId, ActorReference actor,
        DateTimeOffset at)
    {
        var resolvedTimeZoneId = timeZoneId is null
            ? _timeZoneId
            : WorkDigestSchedule.NormalizeTimeZoneId(timeZoneId);
        if (timeZoneId is not null && !WorkDigestSchedule.IsValidTimeZone(timeZoneId))
            throw new ArgumentException("The time zone is invalid.", nameof(timeZoneId));
        if (emailDigestEnabled == _emailDigestEnabled && resolvedTimeZoneId == _timeZoneId)
            return;
        RaiseEvent(new WorkDigestPreferenceChanged(_tenantId, Id, emailDigestEnabled, actor, at)
        {
            TimeZoneId = resolvedTimeZoneId,
        });
    }
}
